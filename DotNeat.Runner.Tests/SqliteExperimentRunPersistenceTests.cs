using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DotNeat.Runner.Persistence;

namespace DotNeat.Runner.Tests;

[TestClass]
public class SqliteExperimentRunPersistenceTests
{
    private string CreateTempDbPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dotneat_test_{Guid.NewGuid():N}.db");
        return path;
    }

    [TestMethod]
    public void BeginRun_creates_record_and_cleans_previous_unfinished_runs()
    {
        string db = CreateTempDbPath();
        try
        {
            // Create a persistence instance and insert a fake unfinished run directly
            var persistence1 = new SqliteExperimentRunPersistence(db);

            // Directly insert an unfinished run to simulate stale state
            using (var conn = new SqliteConnection($"Data Source={db}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO ExperimentRuns (RunId, ExperimentName, Seed, StartedUtc, Completed, ConfigJson) VALUES ('00000000-0000-0000-0000-000000000001','x',1,'2020-01-01T00:00:00Z',0,'{}');";
                cmd.ExecuteNonQuery();
            }

            var context = new EvolutionRunContext("", 123, "{}") { ExperimentName = "test" };
            var runId = persistence1.BeginRun(context);

            using (var conn = new SqliteConnection($"Data Source={db}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM ExperimentRuns WHERE RunId = $rid;";
                cmd.Parameters.AddWithValue("$rid", runId.ToString("D"));
                var count = (long)cmd.ExecuteScalar()!;
                Assert.AreEqual(1, count);

                // Ensure no unfinished runs remain (cleanup deleted previous with Completed=0)
                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT COUNT(*) FROM ExperimentRuns WHERE Completed = 0;";
                var unfinished = (long)cmd2.ExecuteScalar()!;
                Assert.AreEqual(1, unfinished, "There should only be the newly created unfinished run");
            }
        }
        finally
        {
            try { File.Delete(db); } catch { }
        }
    }

    [TestMethod]
    public void PersistGeneration_inserts_and_upserts_generation_rows()
    {
        string db = CreateTempDbPath();
        try
        {
            var persistence = new SqliteExperimentRunPersistence(db);
            var context = new EvolutionRunContext("car", 42, "{}");
            var runId = persistence.BeginRun(context);

            var genome = new Genome();
            var metrics = new GenerationMetrics
            {
                Generation = 0,
                BestFitness = 1.5,
                AverageFitness = 0.5,
                SpeciesCount = 2,
                AverageComplexity = 3.0,
                PopulationSize = 10
            };

            persistence.PersistGeneration(runId, metrics, genome);

            // Verify inserted
            using (var conn = new SqliteConnection($"Data Source={db}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT BestFitness FROM Generations WHERE RunId = $rid AND GenerationIndex = 0;";
                cmd.Parameters.AddWithValue("$rid", runId.ToString("D"));
                var best = (double)cmd.ExecuteScalar()!;
                Assert.AreEqual(1.5, best, 1e-9);
            }

            // Upsert with different best fitness
            metrics.BestFitness = 2.75;
            persistence.PersistGeneration(runId, metrics, genome);

            using (var conn = new SqliteConnection($"Data Source={db}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT BestFitness FROM Generations WHERE RunId = $rid AND GenerationIndex = 0;";
                cmd.Parameters.AddWithValue("$rid", runId.ToString("D"));
                var best = (double)cmd.ExecuteScalar()!;
                Assert.AreEqual(2.75, best, 1e-9);
            }
        }
        finally
        {
            try { File.Delete(db); } catch { }
        }
    }

    [TestMethod]
    public void CompleteRun_sets_completed_and_finished_and_bestfitness()
    {
        string db = CreateTempDbPath();
        try
        {
            var persistence = new SqliteExperimentRunPersistence(db);
            var context = new EvolutionRunContext("car", 7, "{}");
            var runId = persistence.BeginRun(context);

            var finished = DateTime.UtcNow;
            persistence.CompleteRun(runId, 9.5, finished);

            using (var conn = new SqliteConnection($"Data Source={db}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Completed, BestFitness, FinishedUtc FROM ExperimentRuns WHERE RunId = $rid;";
                cmd.Parameters.AddWithValue("$rid", runId.ToString("D"));
                using var reader = cmd.ExecuteReader();
                Assert.IsTrue(reader.Read());
                var completed = reader.GetInt32(0);
                var best = reader.GetDouble(1);
                var finishedUtcStr = reader.GetString(2);
                Assert.AreEqual(1, completed);
                Assert.AreEqual(9.5, best, 1e-9);
                // parse finishedUtcStr
                var parsed = DateTime.Parse(finishedUtcStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
                Assert.AreEqual(finished.ToString("O"), parsed.ToString("O"));
            }
        }
        finally
        {
            try { File.Delete(db); } catch { }
        }
    }
}
