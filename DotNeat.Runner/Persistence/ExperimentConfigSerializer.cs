using System.Text.Json;

namespace DotNeat.Runner.Persistence;

internal static class ExperimentConfigSerializer
{
    public static string Serialize(EvolutionOptions options, object? experimentSpecific = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        object payload = new
        {
            options.PopulationSize,
            options.GenerationCount,
            options.CompatibilityThreshold,
            options.C1,
            options.C2,
            options.C3,
            options.Seed,
            Reproduction = new
            {
                options.Reproduction.ElitesPerSpecies,
                options.Reproduction.TournamentSize,
                options.Reproduction.CrossoverProbability,
                options.Reproduction.MutationProbability,
                options.Reproduction.WeightMutationProbability,
                options.Reproduction.AddConnectionMutationProbability,
                options.Reproduction.AddNodeMutationProbability,
                options.Reproduction.ToggleConnectionMutationProbability,
                options.Reproduction.WeightPerturbChance,
                options.Reproduction.WeightPerturbScale,
                options.Reproduction.WeightResetMin,
                options.Reproduction.WeightResetMax,
                options.Reproduction.BiasMutationChance,
                options.Reproduction.BiasPerturbChance,
                options.Reproduction.BiasPerturbScale,
                options.Reproduction.BiasResetMin,
                options.Reproduction.BiasResetMax,
                options.Reproduction.NewNodeBiasMin,
                options.Reproduction.NewNodeBiasMax,
            },
            ExperimentSpecific = experimentSpecific,
        };

        return JsonSerializer.Serialize(payload);
    }
}
