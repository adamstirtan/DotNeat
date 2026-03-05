<script setup lang="ts">
import type { ExperimentRun } from '@/types/replay'

defineProps<{
  runs: ExperimentRun[]
  selectedRunId: string | null
  loading: boolean
}>()

const RUN_ID_PREFIX_LENGTH = 8

const emit = defineEmits<{
  select: [runId: string]
}>()

function toDate(value: string | null): string {
  if (!value) {
    return 'In progress'
  }

  return new Date(value).toLocaleString()
}

function runLabel(run: ExperimentRun): string {
  return `${run.ExperimentName} · seed ${run.Seed} · ${run.RunId.slice(0, RUN_ID_PREFIX_LENGTH)}`
}
</script>

<template>
  <v-card title="Experiment runs" subtitle="Choose a run to replay generation-by-generation.">
    <template #text>
      <v-alert v-if="loading" density="compact" type="info" variant="tonal">
        Loading runs from experiments.db…
      </v-alert>
      <v-alert v-else-if="runs.length === 0" density="compact" type="warning" variant="tonal">
        No runs found. A starter experiment will be generated automatically on first API request.
      </v-alert>

      <v-list density="compact" nav>
        <v-list-item
          v-for="run in runs"
          :key="run.RunId"
          :active="run.RunId === selectedRunId"
          @click="emit('select', run.RunId)"
        >
          <v-list-item-title>
            {{ runLabel(run) }}
          </v-list-item-title>
          <v-list-item-subtitle>
            Started {{ toDate(run.StartedUtc) }} · Finished {{ toDate(run.FinishedUtc) }}
          </v-list-item-subtitle>
          <v-list-item-subtitle>
            Best fitness {{ run.BestFitness?.toFixed(3) ?? 'n/a' }} · RunId {{ run.RunId }}
          </v-list-item-subtitle>
          <template #append>
            <v-chip :color="run.Completed ? 'success' : 'warning'" size="small" variant="outlined">
              {{ run.Completed ? 'Completed' : 'Running' }}
            </v-chip>
          </template>
        </v-list-item>
      </v-list>
    </template>
  </v-card>
</template>
