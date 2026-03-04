# DotNeat

DotNeat is a clean-room, from-scratch implementation of the classic NEAT algorithm (NeuroEvolution of Augmenting Topologies) written entirely in modern C# for .NET.

[![Continuous Integration](https://github.com/adamstirtan/DotNeat/actions/workflows/ci.yaml/badge.svg)](https://github.com/adamstirtan/DotNeat/actions/workflows/ci.yaml)

![f9b259be-c178-434a-b26e-1ee4fddac40f](https://github.com/user-attachments/assets/ba5085df-2e5f-4c8b-af3f-c82daf2c0fce)

NEAT — originally developed by Kenneth O. Stanley — is a powerful neuroevolution method that evolves both the weights and the topology (structure) of neural networks using genetic algorithms. It starts with simple networks and gradually complexifies them by adding nodes and connections, while using historical markings and speciation to protect structural innovation.

## Why DotNeat exists

The project was built primarily for learning, experimentation, and deep understanding of how NEAT actually works under the hood — without relying on existing wrapper libraries or ports.

## Key design choices and focus areas:

- Clarity first — Code is written to be readable and well-commented rather than ultra-optimized. Every major NEAT concept (genomes, phenotypes, speciation, innovation numbers, crossover, mutation, compatibility distance, etc.) is implemented in a traceable way.
- Modern C# & .NET style — Leverages recent language features (records, pattern matching, nullable reference types, init-only properties, etc.) while staying compatible with current .NET versions (likely .NET 6+ / .NET 8+).
- No external dependencies (or very minimal) — Pure .NET implementation so it's easy to understand, fork, modify, or use as a teaching tool.
- Experimentation-friendly — Designed so you can easily tweak mutation probabilities, speciation parameters, activation functions, or even core mechanisms to see what happens.

## Typical use cases people explore with DotNeat-style implementations

- Classic control tasks (pole balancing, mountain car, etc.)
- Visualizing network evolution over generations
- Comparing NEAT variants (e.g., with/without explicit fitness sharing, different crossover strategies)
- Integrating with custom environments or games
- Learning material for people studying neuroevolution, genetic algorithms, or AI without heavy math prerequisites

In short: if you're curious about how one of the most influential neuroevolution algorithms really works — line by line — or want a readable, hackable reference implementation in C#, DotNeat is exactly that kind of project.
