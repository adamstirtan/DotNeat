<script setup lang="ts">
import type { Generation } from '@/types/replay'

const props = defineProps<{
  generation: Generation | null
  firstGeneration: Generation | null
}>()

const metricCards = [
  { label: 'Best fitness', key: 'BestFitness' as const, hint: 'Champion score in this generation.' },
  {
    label: 'Average fitness',
    key: 'AverageFitness' as const,
    hint: 'Population-wide average score for this generation.',
  },
  { label: 'Species count', key: 'SpeciesCount' as const, hint: 'How many species currently exist.' },
  {
    label: 'Average complexity',
    key: 'AverageComplexity' as const,
    hint: 'Mean network complexity in the population.',
  },
  { label: 'Population size', key: 'PopulationSize' as const, hint: 'Total evaluated genomes.' },
]

function valueFor(key: keyof Generation): string {
  const value = props.generation?.[key]
  if (typeof value === 'number') {
    return Number.isInteger(value) ? value.toString() : value.toFixed(3)
  }

  return '—'
}

function deltaFor(key: keyof Generation): string {
  if (!props.generation || !props.firstGeneration) {
    return '—'
  }

  const start = props.firstGeneration[key]
  const current = props.generation[key]
  if (typeof start !== 'number' || typeof current !== 'number') {
    return '—'
  }

  const delta = current - start
  return `${delta >= 0 ? '+' : ''}${Number.isInteger(delta) ? delta : delta.toFixed(3)} vs gen 0`
}
</script>

<template>
  <v-card title="Generation metrics" subtitle="Core indicators teachers can discuss while replaying NEAT evolution.">
    <template #text>
      <v-row>
        <v-col v-for="metric in metricCards" :key="metric.key" cols="12" md="6" lg="4">
          <v-card variant="tonal">
            <v-card-title class="text-subtitle-1">{{ metric.label }}</v-card-title>
            <v-card-subtitle>{{ metric.hint }}</v-card-subtitle>
            <v-card-text>
              <div class="text-h5">{{ valueFor(metric.key) }}</div>
              <div class="text-caption text-medium-emphasis">{{ deltaFor(metric.key) }}</div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </template>
  </v-card>
</template>
