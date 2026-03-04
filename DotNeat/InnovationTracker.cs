namespace DotNeat;

public readonly record struct NodeSplitInnovation(
    Guid NewNodeId,
    int InputConnectionInnovationNumber,
    int OutputConnectionInnovationNumber);

public sealed class InnovationTracker
{
    private readonly Lock _sync = new();
    private readonly Dictionary<(Guid input, Guid output), int> _connectionInnovations = [];
    private readonly Dictionary<int, (Guid input, Guid output)> _innovationToConnection = [];
    private readonly Dictionary<int, NodeSplitInnovation> _nodeSplitInnovations = [];

    private int _nextInnovationNumber;

    public InnovationTracker(int startingInnovationNumber = 1)
    {
        if (startingInnovationNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(startingInnovationNumber), "Starting innovation number must be >= 1.");
        }

        _nextInnovationNumber = startingInnovationNumber;
    }

    public int PeekNextInnovationNumber()
    {
        lock (_sync)
        {
            return _nextInnovationNumber;
        }
    }

    public int GetOrCreateConnectionInnovation(Guid inputNodeId, Guid outputNodeId)
    {
        (Guid input, Guid output) key = (inputNodeId, outputNodeId);

        lock (_sync)
        {
            if (_connectionInnovations.TryGetValue(key, out int existingInnovation))
            {
                return existingInnovation;
            }

            int innovation = _nextInnovationNumber++;
            _connectionInnovations[key] = innovation;
            _innovationToConnection[innovation] = key;
            return innovation;
        }
    }

    public NodeSplitInnovation GetOrCreateNodeSplitInnovation(int splitConnectionInnovationNumber, Func<Guid>? nodeIdFactory = null)
    {
        if (splitConnectionInnovationNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(splitConnectionInnovationNumber), "Connection innovation number must be >= 1.");
        }

        lock (_sync)
        {
            if (_nodeSplitInnovations.TryGetValue(splitConnectionInnovationNumber, out NodeSplitInnovation existingSplit))
            {
                return existingSplit;
            }

            Guid newNodeId = (nodeIdFactory ?? Guid.NewGuid).Invoke();
            int inputConnectionInnovation = _nextInnovationNumber++;
            int outputConnectionInnovation = _nextInnovationNumber++;

            NodeSplitInnovation split = new(
                newNodeId,
                inputConnectionInnovation,
                outputConnectionInnovation);

            _nodeSplitInnovations[splitConnectionInnovationNumber] = split;
            return split;
        }
    }

    public void RegisterConnectionInnovation(Guid inputNodeId, Guid outputNodeId, int innovationNumber)
    {
        if (innovationNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(innovationNumber), "Innovation number must be >= 1.");
        }

        (Guid input, Guid output) key = (inputNodeId, outputNodeId);

        lock (_sync)
        {
            if (_connectionInnovations.TryGetValue(key, out int existingInnovation) && existingInnovation != innovationNumber)
            {
                throw new InvalidOperationException(
                    $"Connection {inputNodeId} -> {outputNodeId} already mapped to innovation {existingInnovation}.");
            }

            if (_innovationToConnection.TryGetValue(innovationNumber, out (Guid input, Guid output) existingConnection) && existingConnection != key)
            {
                throw new InvalidOperationException(
                    $"Innovation {innovationNumber} already mapped to connection {existingConnection.input} -> {existingConnection.output}.");
            }

            _connectionInnovations[key] = innovationNumber;
            _innovationToConnection[innovationNumber] = key;

            if (innovationNumber >= _nextInnovationNumber)
            {
                _nextInnovationNumber = innovationNumber + 1;
            }
        }
    }

    public void RegisterConnectionInnovation(ConnectionGene connection)
    {
        RegisterConnectionInnovation(connection.InputNodeId, connection.OutputNodeId, connection.InnovationNumber);
    }
}
