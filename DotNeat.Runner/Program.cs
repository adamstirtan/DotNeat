using DotNeat.Runner.Experiments;

string experimentName = args.Length > 0 ? args[0].ToLowerInvariant() : "xor";
int seed = args.Length > 1 && int.TryParse(args[1], out int parsedSeed) ? parsedSeed : 12345;

IExperiment experiment = experimentName switch
{
    "cartpole" => new CartPoleExperiment(seed),
    _ => new XorExperiment(seed),
};

experiment.Run();
