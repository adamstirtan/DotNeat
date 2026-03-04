using Microsoft.Data.Sqlite;
using System.Globalization;

namespace DotNeat.Runner.Persistence;

public sealed class SqliteExperimentRunPersistence : IEvolutionRunPersistence
{
    private readonly string _connectionString;

    public string DatabasePath { get; }

    public SqliteExperimentRunPersistence(string? databasePath = null)
    {
        DatabasePath = databasePath ?? Path.Combine(ResolveRepositoryRoot(), "experiments.db");
        _connectionString = $"Data Source={DatabasePath}";
        InitializeDatabase();
    }

    public Guid BeginRun(EvolutionRunContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Guid runId = Guid.NewGuid();
        DateTime startedUtc = DateTime.UtcNow;

        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        using (SqliteCommand cleanupCommand = connection.CreateCommand())
        {
            cleanupCommand.Transaction = transaction;
            cleanupCommand.CommandText = "DELETE FROM ExperimentRuns WHERE Completed = 0;";
            _ = cleanupCommand.ExecuteNonQuery();
        }

        using (SqliteCommand insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO ExperimentRuns (
                    RunId,
                    ExperimentName,
                    Seed,
                    StartedUtc,
                    Completed,
                    ConfigJson
                ) VALUES (
                    $runId,
                    $experimentName,
                    $seed,
                    $startedUtc,
                    0,
                    $configJson
                );
                """;

            _ = insertCommand.Parameters.AddWithValue("$runId", runId.ToString("D"));
            _ = insertCommand.Parameters.AddWithValue("$experimentName", context.ExperimentName);
            _ = insertCommand.Parameters.AddWithValue("$seed", context.Seed);
            _ = insertCommand.Parameters.AddWithValue("$startedUtc", startedUtc.ToString("O", CultureInfo.InvariantCulture));
            _ = insertCommand.Parameters.AddWithValue("$configJson", context.ConfigJson);
            _ = insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return runId;
    }

    public void PersistGeneration(Guid runId, GenerationMetrics metrics, Genome generationChampion)
    {
        ArgumentNullException.ThrowIfNull(generationChampion);

        string genomeJson = ExperimentGenomeSerializer.Serialize(generationChampion);

        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO Generations (
                RunId,
                GenerationIndex,
                BestFitness,
                AverageFitness,
                SpeciesCount,
                AverageComplexity,
                PopulationSize,
                BestGenomeJson
            ) VALUES (
                $runId,
                $generationIndex,
                $bestFitness,
                $averageFitness,
                $speciesCount,
                $averageComplexity,
                $populationSize,
                $bestGenomeJson
            )
            ON CONFLICT(RunId, GenerationIndex) DO UPDATE SET
                BestFitness = excluded.BestFitness,
                AverageFitness = excluded.AverageFitness,
                SpeciesCount = excluded.SpeciesCount,
                AverageComplexity = excluded.AverageComplexity,
                PopulationSize = excluded.PopulationSize,
                BestGenomeJson = excluded.BestGenomeJson;
            """;

        _ = command.Parameters.AddWithValue("$runId", runId.ToString("D"));
        _ = command.Parameters.AddWithValue("$generationIndex", metrics.Generation);
        _ = command.Parameters.AddWithValue("$bestFitness", metrics.BestFitness);
        _ = command.Parameters.AddWithValue("$averageFitness", metrics.AverageFitness);
        _ = command.Parameters.AddWithValue("$speciesCount", metrics.SpeciesCount);
        _ = command.Parameters.AddWithValue("$averageComplexity", metrics.AverageComplexity);
        _ = command.Parameters.AddWithValue("$populationSize", metrics.PopulationSize);
        _ = command.Parameters.AddWithValue("$bestGenomeJson", genomeJson);

        _ = command.ExecuteNonQuery();
    }

    public void CompleteRun(Guid runId, double bestFitness, DateTime finishedUtc)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            UPDATE ExperimentRuns
            SET
                FinishedUtc = $finishedUtc,
                Completed = 1,
                BestFitness = $bestFitness
            WHERE RunId = $runId;
            """;

        _ = command.Parameters.AddWithValue("$runId", runId.ToString("D"));
        _ = command.Parameters.AddWithValue("$finishedUtc", finishedUtc.ToString("O", CultureInfo.InvariantCulture));
        _ = command.Parameters.AddWithValue("$bestFitness", bestFitness);
        _ = command.ExecuteNonQuery();
    }

    private void InitializeDatabase()
    {
        using SqliteConnection connection = OpenConnection();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS ExperimentRuns (
                RunId TEXT NOT NULL PRIMARY KEY,
                ExperimentName TEXT NOT NULL,
                Seed INTEGER NOT NULL,
                StartedUtc TEXT NOT NULL,
                FinishedUtc TEXT NULL,
                Completed INTEGER NOT NULL,
                BestFitness REAL NULL,
                ConfigJson TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Generations (
                RunId TEXT NOT NULL,
                GenerationIndex INTEGER NOT NULL,
                BestFitness REAL NOT NULL,
                AverageFitness REAL NOT NULL,
                SpeciesCount INTEGER NOT NULL,
                AverageComplexity REAL NOT NULL,
                PopulationSize INTEGER NOT NULL,
                BestGenomeJson TEXT NOT NULL,
                PRIMARY KEY (RunId, GenerationIndex),
                FOREIGN KEY (RunId) REFERENCES ExperimentRuns(RunId) ON DELETE CASCADE
            );
            """;

        _ = command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(_connectionString);
        connection.Open();

        using SqliteCommand pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
        _ = pragmaCommand.ExecuteNonQuery();

        return connection;
    }

    private static string ResolveRepositoryRoot()
    {
        string? root = FindRepositoryRoot(AppContext.BaseDirectory)
            ?? FindRepositoryRoot(Environment.CurrentDirectory);

        return root ?? Environment.CurrentDirectory;
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        DirectoryInfo? directory = new(Path.GetFullPath(startPath));
        if (!directory.Exists)
        {
            return null;
        }

        while (directory is not null)
        {
            string fullName = directory.FullName;
            bool hasGit = Directory.Exists(Path.Combine(fullName, ".git"));
            bool hasSolution = File.Exists(Path.Combine(fullName, "DotNeat.slnx"));

            if (hasGit || hasSolution)
            {
                return fullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
