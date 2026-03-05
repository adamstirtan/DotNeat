<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import LearningPanel from '@/components/LearningPanel.vue'
import GenomeViewer from '@/components/GenomeViewer.vue'
import MetricsPanel from '@/components/MetricsPanel.vue'
import ReplayControls from '@/components/ReplayControls.vue'
import RunSelector from '@/components/RunSelector.vue'
import { fetchGenerations, fetchRuns } from '@/services/replayApi'
import type { ExperimentRun, Generation, Genome } from '@/types/replay'

const allRuns = ref<ExperimentRun[]>([])
const generations = ref<Generation[]>([])
const selectedRunId = ref<string | null>(null)
const generationIndex = ref(0)
const loadingRuns = ref(true)
const loadingGenerations = ref(false)
const errorMessage = ref('')
const autoplay = ref(false)

const searchText = ref('')
const experimentFilter = ref('all')
const completedFilter = ref('all')
const sortBy = ref('started-desc')
const AUTOPLAY_INTERVAL_MS = 900

let autoplayHandle: number | undefined

const experiments = computed(() => [...new Set(allRuns.value.map((run) => run.ExperimentName))].sort())

const filteredRuns = computed(() => {
  const normalizedSearch = searchText.value.trim().toLowerCase()
  return [...allRuns.value]
    .filter((run) => {
      if (experimentFilter.value !== 'all' && run.ExperimentName !== experimentFilter.value) {
        return false
      }

      if (completedFilter.value === 'completed' && run.Completed !== 1) {
        return false
      }

      if (completedFilter.value === 'incomplete' && run.Completed !== 0) {
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
const firstGeneration = computed(() => generations.value[0] ?? null)

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

const generationDeltaText = computed(() => {
  const current = selectedGeneration.value
  if (!current) {
    return 'Pick a run to begin a replay walkthrough.'
  }

  const previous = generations.value[generationIndex.value - 1]
  if (!previous) {
    return 'Generation 0 is your baseline. Explain the starting topology and its initial performance.'
  }

  const fitnessDelta = current.BestFitness - previous.BestFitness
  const speciesDelta = current.SpeciesCount - previous.SpeciesCount
  const complexityDelta = current.AverageComplexity - previous.AverageComplexity

  return `From generation ${previous.GenerationIndex} to ${current.GenerationIndex}: best fitness ${formatSigned(fitnessDelta)}, species ${formatSigned(speciesDelta)}, average complexity ${formatSigned(complexityDelta)}. Discuss why these shifts likely happened.`
})

function formatSigned(value: number): string {
  const normalized = Number.isInteger(value) ? value.toString() : value.toFixed(3)
  return `${value >= 0 ? '+' : ''}${normalized}`
}

async function loadRuns(): Promise<void> {
  loadingRuns.value = true
  errorMessage.value = ''

  try {
    allRuns.value = await fetchRuns()
    const firstRun = allRuns.value[0]
    if (firstRun && !selectedRunId.value) {
      selectedRunId.value = firstRun.RunId
    }
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to load experiment runs.'
  } finally {
    loadingRuns.value = false
  }
}

async function loadGenerations(runId: string): Promise<void> {
  loadingGenerations.value = true
  errorMessage.value = ''
  autoplay.value = false

  try {
    generations.value = await fetchGenerations(runId)
    generationIndex.value = 0
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to load generations.'
  } finally {
    loadingGenerations.value = false
  }
}

function movePrevious(): void {
  generationIndex.value = Math.max(0, generationIndex.value - 1)
}

function moveNext(): void {
  generationIndex.value = Math.min(generations.value.length - 1, generationIndex.value + 1)
}

function seekGeneration(value: number): void {
  generationIndex.value = Math.min(generations.value.length - 1, Math.max(0, Math.round(value)))
}

watch(selectedRunId, (runId) => {
  if (runId) {
    void loadGenerations(runId)
  }
})

watch(autoplay, (enabled) => {
  if (autoplayHandle) {
    window.clearInterval(autoplayHandle)
    autoplayHandle = undefined
  }

  if (enabled) {
    autoplayHandle = window.setInterval(() => {
      if (generationIndex.value >= generations.value.length - 1) {
        autoplay.value = false
        return
      }

      moveNext()
    }, AUTOPLAY_INTERVAL_MS)
  }
})

onMounted(() => {
  void loadRuns()
})

onBeforeUnmount(() => {
  if (autoplayHandle) {
    window.clearInterval(autoplayHandle)
  }
})
</script>

<template>
  <v-app>
    <v-main>
      <v-container class="py-5" fluid>
        <v-row class="mb-4" dense>
          <v-col cols="12">
            <h1 class="text-h4 mb-2">DotNeat experiment replay classroom</h1>
            <p class="text-medium-emphasis">
              Select a run, replay each generation, and teach how fitness, diversity, and topology evolve.
            </p>
          </v-col>
        </v-row>

        <v-alert v-if="errorMessage" class="mb-4" type="error" variant="tonal">{{ errorMessage }}</v-alert>

        <v-row>
          <v-col cols="12" lg="4">
            <v-card class="mb-4" title="Filter runs">
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
                  v-model="completedFilter"
                  :items="['all', 'completed', 'incomplete']"
                  class="mt-3"
                  density="compact"
                  hide-details
                  label="Completed status"
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
              @select="selectedRunId = $event"
            />
          </v-col>

          <v-col cols="12" lg="8">
            <ReplayControls
              v-if="generations.length > 0"
              class="mb-4"
              :autoplay="autoplay"
              :generation-index="generationIndex"
              :max-generation="Math.max(0, generations.length - 1)"
              @next="moveNext"
              @previous="movePrevious"
              @seek="seekGeneration"
              @toggle-autoplay="autoplay = !autoplay"
            />

            <v-alert v-if="loadingGenerations" class="mb-4" type="info" variant="tonal">
              Loading generations for selected run…
            </v-alert>

            <MetricsPanel
              v-if="selectedGeneration"
              class="mb-4"
              :first-generation="firstGeneration"
              :generation="selectedGeneration"
            />

            <GenomeViewer class="mb-4" :genome="parsedGenome" />

            <LearningPanel :generation-delta-text="generationDeltaText" />
          </v-col>
        </v-row>
      </v-container>
    </v-main>
  </v-app>
</template>
