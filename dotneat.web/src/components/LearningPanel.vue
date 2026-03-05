<script setup lang="ts">
defineProps<{
  generationDeltaText: string
}>()

const explanations = [
  {
    title: 'Best fitness',
    description: 'Shows the strongest individual. Growth means evolution discovered a better policy.',
  },
  {
    title: 'Average fitness',
    description: 'Shows if improvement is shared across the population, not just one champion.',
  },
  {
    title: 'Species count',
    description: 'Represents diversity. Sudden drops can indicate convergence; increases suggest innovation.',
  },
  {
    title: 'Average complexity',
    description: 'Tracks structural growth. NEAT should complexify only when it helps fitness.',
  },
]
</script>

<template>
  <v-card title="Teaching notes" subtitle="Learning-first guidance for explaining each replay step.">
    <template #text>
      <v-alert class="mb-4" density="comfortable" type="info" variant="tonal">
        {{ generationDeltaText }}
      </v-alert>
      <v-expansion-panels>
        <v-expansion-panel v-for="item in explanations" :key="item.title">
          <v-expansion-panel-title>{{ item.title }}</v-expansion-panel-title>
          <v-expansion-panel-text>{{ item.description }}</v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>
    </template>
  </v-card>
</template>
