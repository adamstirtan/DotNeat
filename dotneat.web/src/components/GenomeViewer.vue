<script setup lang="ts">
import { computed } from 'vue'
import type { Genome } from '@/types/replay'

const props = defineProps<{
  genome: Genome | null
}>()

type PositionedNode = {
  id: string
  x: number
  y: number
  type: number
}

const nodeGroups = computed(() => {
  const nodes = props.genome?.Nodes ?? []
  const inputs = nodes.filter((node) => node.NodeType === 0)
  const hidden = nodes.filter((node) => node.NodeType === 2)
  const outputs = nodes.filter((node) => node.NodeType === 1)
  return [inputs, hidden, outputs]
})

const positionedNodes = computed<PositionedNode[]>(() => {
  const xPositions: Record<number, number> = {
    0: 0.1,
    1: 0.5,
    2: 0.9,
  }

  return nodeGroups.value.flatMap((group, groupIndex) => {
    if (group.length === 0) {
      return []
    }

    return group.map((node, index) => {
      const y = (index + 1) / (group.length + 1)
      return {
        id: node.NodeId,
        x: xPositions[groupIndex] ?? 0.5,
        y,
        type: node.NodeType,
      }
    })
  })
})

const nodeMap = computed(() => new Map(positionedNodes.value.map((node) => [node.id, node])))

const edges = computed(() => {
  const connections = props.genome?.Connections ?? []
  return connections
    .map((connection) => {
      const source = nodeMap.value.get(connection.InputNodeId)
      const target = nodeMap.value.get(connection.OutputNodeId)
      if (!source || !target) {
        return null
      }

      return {
        id: connection.ConnectionId,
        x1: source.x,
        y1: source.y,
        x2: target.x,
        y2: target.y,
        weight: connection.Weight,
        enabled: connection.Enabled,
      }
    })
    .filter((edge): edge is NonNullable<typeof edge> => edge !== null)
})

function nodeColor(nodeType: number): string {
  if (nodeType === 0) return '#2196f3'
  if (nodeType === 1) return '#ff9800'
  return '#7e57c2'
}

function edgeColor(weight: number, enabled: boolean): string {
  if (!enabled) return '#9e9e9e'
  return weight >= 0 ? '#4caf50' : '#f44336'
}
</script>

<template>
  <v-card title="Champion genome" subtitle="Inputs (blue), hidden (purple), outputs (orange).">
    <template #text>
      <v-alert v-if="!genome" density="compact" type="info" variant="tonal">
        Select a run and generation to visualize the champion topology.
      </v-alert>
      <svg v-else class="genome-canvas" viewBox="0 0 1000 500" xmlns="http://www.w3.org/2000/svg">
        <line
          v-for="edge in edges"
          :key="edge.id"
          :stroke="edgeColor(edge.weight, edge.enabled)"
          :stroke-opacity="edge.enabled ? 0.85 : 0.4"
          :stroke-width="Math.min(6, Math.max(1.5, Math.abs(edge.weight) * 1.1))"
          :x1="edge.x1 * 1000"
          :x2="edge.x2 * 1000"
          :y1="edge.y1 * 500"
          :y2="edge.y2 * 500"
        />

        <g v-for="node in positionedNodes" :key="node.id">
          <circle :cx="node.x * 1000" :cy="node.y * 500" :fill="nodeColor(node.type)" r="18" />
          <text :x="node.x * 1000" :y="node.y * 500 + 4" fill="white" font-size="11" text-anchor="middle">
            {{ node.id.slice(0, 4) }}
          </text>
        </g>
      </svg>

      <div class="legend text-caption mt-2">
        Green edge = positive weight · Red edge = negative weight · Gray edge = disabled gene.
      </div>
    </template>
  </v-card>
</template>

<style scoped>
.genome-canvas {
  width: 100%;
  min-height: 360px;
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  background: #101217;
}

.legend {
  color: rgba(255, 255, 255, 0.7);
}
</style>
