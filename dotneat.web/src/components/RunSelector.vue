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
  delete: [runId: string]
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

function requestDelete(runId: string, event: Event): void {
  event.stopPropagation()
  emit('delete', runId)
}
</script>

<template>
  <v-card title="Experiment runs" elevation="0">
    <template #text>
      <v-alert v-if="loading" density="compact" type="info" variant="tonal">
        Loading runs from experiments.db…
      </v-alert>
      <v-alert v-else-if="runs.length === 0" density="compact" type="warning" variant="tonal">
        No runs found. A starter experiment will be generated automatically on first API request.
      </v-alert>

      <v-list>
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
          <template v-slot:append>
            <v-btn
              variant="text"
              color="error"
              aria-label="Delete run"
              @click="requestDelete(run.RunId, $event)"
            >
              Delete
            </v-btn>
          </template>
        </v-list-item>
      </v-list>
    </template>
  </v-card>
</template>

