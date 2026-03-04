using DotNeat;

namespace DotNeat.Runner.Sim;

/// <summary>
/// A single recorded simulation frame for the top-down car experiment.
/// </summary>
public sealed record CarFrame(
    double X,
    double Y,
    double Heading,
    double Speed,
    double[] Sensors,
    double Steering,
    double Throttle);

/// <summary>
/// Records a full episode trajectory for visualization purposes, reusing the
/// physics and sensor model from <see cref="CarFitnessEvaluator"/>.
/// </summary>
public static class CarSimulator
{
    /// <summary>
    /// Runs the champion genome in the simulator and records one <see cref="CarFrame"/>
    /// per step so the result can be replayed in the browser UI.
    /// </summary>
    public static IReadOnlyList<CarFrame> RecordEpisode(
        NeuralNetwork network,
        double goalX,
        double goalY,
        double startX = CarFitnessEvaluator.DefaultStartX,
        double startY = CarFitnessEvaluator.DefaultStartY,
        int maxSteps = 400)
    {
        ArgumentNullException.ThrowIfNull(network);

        List<CarFrame> frames = new(maxSteps);

        double x = startX;
        double y = startY;
        double heading = 0.0;
        double speed = 0.0;

        IReadOnlyList<Guid> inputIds = network.InputNodeIds;
        Guid steeringId = network.OutputNodeIds[0];
        Guid throttleId = network.OutputNodeIds[1];

        for (int step = 0; step < maxSteps; step++)
        {
            double[] rays = CarFitnessEvaluator.CastRays(x, y, heading);
            double goalAngle = NormalizeAngle(Math.Atan2(goalY - y, goalX - x) - heading);
            double goalDist = Math.Clamp(
                Euclidean(x, y, goalX, goalY) / (CarFitnessEvaluator.ArenaWidth * 0.8),
                0.0, 1.0);

            Dictionary<Guid, double> inputMap = new(7)
            {
                [inputIds[0]] = rays[0],
                [inputIds[1]] = rays[1],
                [inputIds[2]] = rays[2],
                [inputIds[3]] = rays[3],
                [inputIds[4]] = rays[4],
                [inputIds[5]] = goalAngle / Math.PI,
                [inputIds[6]] = goalDist,
            };

            IReadOnlyDictionary<Guid, double> outputMap = network.Forward(inputMap);
            double steeringOutput = outputMap[steeringId];
            double throttleOutput = outputMap[throttleId];

            frames.Add(new CarFrame(x, y, heading, speed, rays, steeringOutput, throttleOutput));

            (x, y, heading, speed) = CarFitnessEvaluator.PhysicsStep(
                x, y, heading, speed, steeringOutput, throttleOutput);

            if (Euclidean(x, y, goalX, goalY) < CarFitnessEvaluator.GoalRadius)
            {
                frames.Add(new CarFrame(x, y, heading, speed, rays, steeringOutput, throttleOutput));
                break;
            }

            if (x < 0 || x > CarFitnessEvaluator.ArenaWidth ||
                y < 0 || y > CarFitnessEvaluator.ArenaHeight)
            {
                break;
            }
        }

        return frames;
    }

    private static double Euclidean(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= 2.0 * Math.PI;
        }

        while (angle < -Math.PI)
        {
            angle += 2.0 * Math.PI;
        }

        return angle;
    }
}
