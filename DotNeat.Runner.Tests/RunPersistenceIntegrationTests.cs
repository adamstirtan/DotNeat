using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DotNeat.Runner.Persistence;

namespace DotNeat.Runner.Tests;

[TestClass]
public class RunPersistenceIntegrationTests
{
    [TestMethod]
    public void Full_run_begin_persist_complete_roundtrip()
    {
        string db = Path.Combine(Path.GetTempPath(), $"dotneat_runint_{Guid.NewGuid():N}.db");
        try
        {
            var persistence = new SqliteExperimentRunPersistence(db);
            var context = new EvolutionRunContext("integration-experiment", 1234, "{\"param\":true}");
            var runId = persistence.BeginRun(context);

            // Persist multiple generations
            for (int g = 0; g < 3; g++)
            {
                var genome = new Genome();
                var metrics = new GenerationMetrics
                {
                    Generation = g,
                    BestFitness = 1.0 + g,
                    AverageFitness = 0.5 + 0.1 * g,
                    SpeciesCount = 1 + g,
                    AverageComplexity = 2 + g,
                    PopulationSize = 10
                };

                persistence.PersistGeneration(runId, metrics, genome);
            }

            // Complete run
            double finalBest = 42.42;
            var finished = DateTime.UtcNow;
            persistence.CompleteRun(runId, finalBest, finished);

            // Verify ExperimentRuns row
            using (var conn = new SqliteConnection($"Data Source={db}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Completed, BestFitness, ConfigJson FROM ExperimentRuns WHERE RunId = $rid;";
                cmd.Parameters.AddWithValue("$rid", runId.ToString("D"));
                using var reader = cmd.ExecuteReader();
                Assert.IsTrue(reader.Read(), "ExperimentRuns row missing");
                Assert.AreEqual(1, reader.GetInt32(0));
                Assert.AreEqual(finalBest, reader.GetDouble(1), 1e-9);
                Assert.AreEqual(context.ConfigJson, reader.GetString(2));
            }

            // Verify Generations rows count and values
            using (var conn = new SqliteConnection($"Data Source={db}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Generations WHERE RunId = $rid;";
                cmd.Parameters.AddWithValue("$rid", runId.ToString("D"));
                var count = (long)cmd.ExecuteScalar()!;
                Assert.AreEqual(3, count);

                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT GenerationIndex, BestFitness FROM Generations WHERE RunId = $rid ORDER BY GenerationIndex;";
                cmd2.Parameters.AddWithValue("$rid", runId.ToString("D"));
                using var reader = cmd2.ExecuteReader();
                int idx = 0;
                while (reader.Read())
                {
                    var genIdx = reader.GetInt32(0);
                    var best = reader.GetDouble(1);
                    Assert.AreEqual(idx, genIdx);
                    Assert.AreEqual(1.0 + idx, best, 1e-9);
                    idx++;
                }
            }
        }
        finally
        {
            try { File.Delete(db); } catch { }
        }
    }
}
