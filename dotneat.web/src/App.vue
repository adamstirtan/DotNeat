<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import GenomeViewer from '@/components/GenomeViewer.vue'
import EvolutionChart from '@/components/EvolutionChart.vue'
import MetricsPanel from '@/components/MetricsPanel.vue'
import ReplayControls from '@/components/ReplayControls.vue'
import RunSelector from '@/components/RunSelector.vue'
import { deleteRun as deleteRunApi, fetchGenerations, fetchRuns } from '@/services/replayApi'
import type { ExperimentRun, Generation, Genome } from '@/types/replay'

const allRuns = ref<ExperimentRun[]>([])
const generations = ref<Generation[]>([])
const selectedRunId = ref<string | null>(null)
const generationIndex = ref(0)
const loadingRuns = ref(true)
const loadingGenerations = ref(false)
const errorMessage = ref('')
const runPickerOpen = ref(true)

const searchText = ref('')
const experimentFilter = ref('all')
const sortBy = ref('started-desc')

const experiments = computed(() => [...new Set(allRuns.value.map((run) => run.ExperimentName))].sort())

const filteredRuns = computed(() => {
  const normalizedSearch = searchText.value.trim().toLowerCase()
  return [...allRuns.value]
    .filter((run) => {
      if (run.Completed !== 1) {
        return false
      }

      if (experimentFilter.value !== 'all' && run.ExperimentName !== experimentFilter.value) {
        return false
      }

      if (!normalizedSearch) {
        return true
      }

      return [run.RunId, run.ExperimentName, run.Seed.toString()].some((value) =>
        value.toLowerCase().includes(normalizedSearch),
      )
    })
    .sort((left, right) => {
      if (sortBy.value === 'started-asc') {
        return left.StartedUtc.localeCompare(right.StartedUtc)
      }

      if (sortBy.value === 'best-desc') {
        return (right.BestFitness ?? Number.NEGATIVE_INFINITY) - (left.BestFitness ?? Number.NEGATIVE_INFINITY)
      }

      if (sortBy.value === 'best-asc') {
        return (left.BestFitness ?? Number.POSITIVE_INFINITY) - (right.BestFitness ?? Number.POSITIVE_INFINITY)
      }

      return right.StartedUtc.localeCompare(left.StartedUtc)
    })
})

const selectedGeneration = computed(() => generations.value[generationIndex.value] ?? null)

const parsedGenome = computed<Genome | null>(() => {
  const generation = selectedGeneration.value
  if (!generation) {
    return null
  }

  try {
    return JSON.parse(generation.BestGenomeJson) as Genome
  } catch {
    return null
  }
})

async function loadRuns(): Promise<void> {
  loadingRuns.value = true
  errorMessage.value = ''

  try {
    allRuns.value = await fetchRuns()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to load experiment runs.'
  } finally {
    loadingRuns.value = false
  }
}

async function loadGenerations(runId: string): Promise<void> {
  loadingGenerations.value = true
  errorMessage.value = ''

  try {
    generations.value = await fetchGenerations(runId)
    generationIndex.value = 0
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to load generations.'
  } finally {
    loadingGenerations.value = false
  }
}

function moveNext(): void {
  generationIndex.value = Math.min(generations.value.length - 1, generationIndex.value + 1)
}

function seekGeneration(value: number): void {
  generationIndex.value = Math.min(generations.value.length - 1, Math.max(0, Math.round(value)))
}

function selectRun(runId: string): void {
  selectedRunId.value = runId
  runPickerOpen.value = false
}

async function deleteRun(runId: string): Promise<void> {
  const target = allRuns.value.find((run) => run.RunId === runId)
  const label = target ? `${target.ExperimentName} (seed ${target.Seed})` : runId
  const confirmed = window.confirm(`Delete run ${label}? This cannot be undone.`)

  if (!confirmed) {
    return
  }

  try {
    await deleteRunApi(runId)

    if (selectedRunId.value === runId) {
      selectedRunId.value = null
      generations.value = []
      generationIndex.value = 0
      runPickerOpen.value = true
    }

    await loadRuns()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to delete run.'
  }
}

watch(selectedRunId, (runId) => {
  if (runId) {
    void loadGenerations(runId)
  }
})

onMounted(() => {
  void loadRuns()
})
</script>

<template>
  <v-app>
    <v-app-bar app color="primary" density="comfortable">
      <v-container class="content-container d-flex align-center py-0">
        <v-app-bar-title>DotNeat Experiment Replay</v-app-bar-title>
        <v-spacer />
        <v-btn variant="text" color="white" @click="runPickerOpen = true">Choose run</v-btn>
      </v-container>
    </v-app-bar>

    <v-dialog v-model="runPickerOpen" max-width="900" persistent>
      <v-card>
        <template #text>
          <v-card class="mb-4" elevation="0" title="Filter runs">
            <template #text>
              <v-text-field v-model="searchText" density="compact" hide-details label="Search run id / experiment / seed" />
              <v-select
                v-model="experimentFilter"
                :items="['all', ...experiments]"
                class="mt-3"
                density="compact"
                hide-details
                label="Experiment"
              />
              <v-select
                v-model="sortBy"
                :items="[
                  { title: 'Started (newest first)', value: 'started-desc' },
                  { title: 'Started (oldest first)', value: 'started-asc' },
                  { title: 'Best fitness (high to low)', value: 'best-desc' },
                  { title: 'Best fitness (low to high)', value: 'best-asc' },
                ]"
                class="mt-3"
                density="compact"
                hide-details
                label="Sort by"
              />
            </template>
          </v-card>

          <RunSelector
            :loading="loadingRuns"
            :runs="filteredRuns"
            :selected-run-id="selectedRunId"
            @select="selectRun"
            @delete="deleteRun"
          />
        </template>
      </v-card>
    </v-dialog>

    <v-main>
      <v-container class="content-container py-5">
        <v-alert v-if="errorMessage" class="mb-4" type="error" variant="tonal">{{ errorMessage }}</v-alert>

        <ReplayControls
          v-if="generations.length > 0"
          class="mb-4"
          :generation-index="generationIndex"
          :max-generation="Math.max(0, generations.length - 1)"
          @seek="seekGeneration"
        />

        <v-alert v-if="loadingGenerations" class="mb-4" type="info" variant="tonal">
          Loading generations for selected run…
        </v-alert>

        <v-row class="mb-2 align-stretch" dense>
          <v-col class="d-flex" cols="12" md="6">
            <EvolutionChart
              v-if="generations.length > 0"
              class="mb-4 flex-grow-1"
              :generations="generations"
              :current-generation-index="selectedGeneration?.GenerationIndex ?? 0"
            />
          </v-col>

          <v-col class="d-flex" cols="12" md="6">
            <GenomeViewer v-if="selectedRunId" class="mb-4 flex-grow-1" :genome="parsedGenome" />
          </v-col>
        </v-row>

        <MetricsPanel
          v-if="selectedGeneration"
          class="mb-4"
          :generations="generations"
          :generation="selectedGeneration"
        />

      </v-container>
    </v-main>
  </v-app>
</template>

<style scoped>
.content-container {
  max-width: 1100px;
}
</style>
