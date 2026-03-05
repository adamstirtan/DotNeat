<script setup lang="ts">
import type { Generation } from '@/types/replay'

const props = defineProps<{
  generation: Generation | null
  generations: Generation[]
}>()

const metricCards = [
  { label: 'Best fitness', key: 'BestFitness' as const, color: '#2e7d32' },
  {
    label: 'Average fitness',
    key: 'AverageFitness' as const,
    color: '#1565c0',
  },
  { label: 'Species count', key: 'SpeciesCount' as const, color: '#6a1b9a' },
  {
    label: 'Average complexity',
    key: 'AverageComplexity' as const,
    color: '#ef6c00',
  },
]

function valueFor(key: keyof Generation): string {
  const value = props.generation?.[key]
  if (typeof value === 'number') {
    return Number.isInteger(value) ? value.toString() : value.toFixed(3)
  }

  return '—'
}

function deltaFor(key: keyof Generation): string {
  if (!props.generation || props.generations.length === 0) {
    return '—'
  }

  const start = props.generations[0]?.[key]
  const current = props.generation[key]
  if (typeof start !== 'number' || typeof current !== 'number') {
    return '—'
  }

  const delta = current - start
  return `${delta >= 0 ? '+' : ''}${Number.isInteger(delta) ? delta : delta.toFixed(3)} vs gen 0`
}

function seriesFor(key: keyof Generation): number[] {
  if (!props.generation || props.generations.length === 0) {
    return []
  }

  const currentIndex = props.generations.findIndex((g) => g.GenerationIndex === props.generation?.GenerationIndex)
  const endIndex = currentIndex < 0 ? props.generations.length - 1 : currentIndex

  return props.generations
    .slice(0, endIndex + 1)
    .map((g) => g[key])
    .filter((value): value is number => typeof value === 'number')
}
</script>

<template>
  <v-row>
    <v-col v-for="metric in metricCards" :key="metric.key" cols="12" md="6" lg="3">
      <v-card class="h-100">
        <v-card-title class="text-subtitle-1">{{ metric.label }}</v-card-title>
        <v-card-text>
          <div class="d-flex align-center justify-space-between mb-2">
            <div class="text-h5">{{ valueFor(metric.key) }}</div>
            <div class="text-caption text-medium-emphasis">{{ deltaFor(metric.key) }}</div>
          </div>

          <v-sparkline
            :model-value="seriesFor(metric.key)"
            auto-line-width
            :fill="false"
            line-width="2"
            min="0"
            padding="2"
            :smooth="true"
            :color="metric.color"
          />
        </v-card-text>
      </v-card>
    </v-col>
  </v-row>
</template>
