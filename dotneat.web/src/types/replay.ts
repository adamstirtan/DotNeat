export interface ExperimentRun {
  RunId: string
  ExperimentName: string
  Seed: number
  StartedUtc: string
  FinishedUtc: string | null
  BestFitness: number | null
  Completed: number
}

export interface Generation {
  RunId: string
  GenerationIndex: number
  BestFitness: number
  AverageFitness: number
  SpeciesCount: number
  AverageComplexity: number
  PopulationSize: number
  BestGenomeJson: string
}

export interface GenomeNode {
  NodeId: string
  NodeType: number
  ActivationFunction: string
  Bias: number
}

export interface GenomeConnection {
  ConnectionId: string
  InputNodeId: string
  OutputNodeId: string
  Weight: number
  Enabled: boolean
  InnovationNumber: number
}

export interface Genome {
  GenomeId: string
  Nodes: GenomeNode[]
  Connections: GenomeConnection[]
}
