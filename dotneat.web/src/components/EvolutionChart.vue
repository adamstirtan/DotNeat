<script setup lang="ts">
import { computed } from 'vue'
import type { Generation } from '@/types/replay'

const props = defineProps<{
  generations: Generation[]
  currentGenerationIndex: number
}>()

const WIDTH = 920
const HEIGHT = 400
const LEFT = 52
const RIGHT = 22
const TOP = 20
const BOTTOM = 36

const chartData = computed(() => {
  if (props.generations.length === 0) {
    return null
  }

  const points = props.generations.map((g) => ({
    x: g.GenerationIndex,
    best: g.BestFitness,
    avg: g.AverageFitness,
  }))

  const firstPoint = points[0]
  const lastPoint = points[points.length - 1]
  if (!firstPoint || !lastPoint) {
    return null
  }

  const xMin = firstPoint.x
  const xMax = lastPoint.x

  let yMin = Math.min(...points.map((p) => Math.min(p.best, p.avg)))
  let yMax = Math.max(...points.map((p) => Math.max(p.best, p.avg)))

  if (Math.abs(yMax - yMin) < 1e-9) {
    yMax = yMin + 1
  }

  const yPad = (yMax - yMin) * 0.1
  yMin -= yPad
  yMax += yPad

  const plotX = (x: number): number => {
    if (xMax === xMin) {
      return LEFT
    }

    return LEFT + ((x - xMin) / (xMax - xMin)) * (WIDTH - LEFT - RIGHT)
  }

  const plotY = (y: number): number => TOP + ((yMax - y) / (yMax - yMin)) * (HEIGHT - TOP - BOTTOM)

  const bestPolyline = points.map((p) => `${plotX(p.x).toFixed(2)},${plotY(p.best).toFixed(2)}`).join(' ')
  const avgPolyline = points.map((p) => `${plotX(p.x).toFixed(2)},${plotY(p.avg).toFixed(2)}`).join(' ')

  const clampedGeneration = Math.max(xMin, Math.min(xMax, props.currentGenerationIndex))
  const markerX = plotX(clampedGeneration)

  return {
    xMin,
    xMax,
    yMin,
    yMax,
    bestPolyline,
    avgPolyline,
    markerX,
  }
})
</script>

<template>
  <v-card class="h-100 d-flex flex-column" title="Fitness progression" subtitle="Best and average fitness over generations.">
    <template #text>
      <v-alert v-if="!chartData" density="compact" type="info" variant="tonal">
        Select a run to view charted evolution metrics.
      </v-alert>

      <div v-else class="chart-wrapper">
        <svg class="chart-canvas" :viewBox="`0 0 ${WIDTH} ${HEIGHT}`" role="img" aria-label="Fitness chart">
          <line :x1="LEFT" :y1="HEIGHT - BOTTOM" :x2="WIDTH - RIGHT" :y2="HEIGHT - BOTTOM" stroke="#9ca3af" />
          <line :x1="LEFT" :y1="TOP" :x2="LEFT" :y2="HEIGHT - BOTTOM" stroke="#9ca3af" />

          <line
            :x1="chartData.markerX"
            :x2="chartData.markerX"
            :y1="TOP"
            :y2="HEIGHT - BOTTOM"
            stroke="#ef6c00"
            stroke-dasharray="5,4"
            stroke-width="2"
          />

          <polyline :points="chartData.bestPolyline" fill="none" stroke="#2e7d32" stroke-width="2.2" />
          <polyline :points="chartData.avgPolyline" fill="none" stroke="#1565c0" stroke-width="2.2" />

          <rect :x="WIDTH - RIGHT - 130" :y="TOP + 2" width="10" height="10" fill="#2e7d32" />
          <text :x="WIDTH - RIGHT - 114" :y="TOP + 11" fill="#374151" font-size="13">Best fitness</text>

          <rect :x="WIDTH - RIGHT - 130" :y="TOP + 18" width="10" height="10" fill="#1565c0" />
          <text :x="WIDTH - RIGHT - 114" :y="TOP + 27" fill="#374151" font-size="13">Average fitness</text>

          <text :x="LEFT + 4" :y="TOP + 12" fill="#6b7280" font-size="13">{{ chartData.yMax.toFixed(2) }}</text>
          <text :x="LEFT + 4" :y="HEIGHT - BOTTOM - 4" fill="#6b7280" font-size="13">{{ chartData.yMin.toFixed(2) }}</text>
        </svg>
      </div>
    </template>
  </v-card>
</template>

<style scoped>
.chart-wrapper {
  height: 100%;
}

.chart-canvas {
  width: 100%;
  height: 100%;
  min-height: 220px;
}

:deep(.v-card-text) {
  display: flex;
  flex: 1;
  flex-direction: column;
}
</style>
