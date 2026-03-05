<script setup lang="ts">
defineProps<{
  generationIndex: number
  maxGeneration: number
  autoplay: boolean
}>()

const emit = defineEmits<{
  previous: []
  next: []
  seek: [value: number]
  toggleAutoplay: []
}>()

function onSeek(value: number | readonly number[]): void {
  if (Array.isArray(value)) {
    emit('seek', Number(value[0] ?? 0))
    return
  }

  emit('seek', Number(value))
}
</script>

<template>
  <v-card title="Replay controls" subtitle="Pause and explain each generation, or autoplay through the timeline.">
    <template #text>
      <v-row class="mb-2" dense>
        <v-col>
          <v-btn block prepend-icon="mdi-skip-previous" @click="emit('previous')">Previous</v-btn>
        </v-col>
        <v-col>
          <v-btn block prepend-icon="mdi-skip-next" @click="emit('next')">Next</v-btn>
        </v-col>
        <v-col>
          <v-btn
            block
            :color="autoplay ? 'warning' : 'primary'"
            :prepend-icon="autoplay ? 'mdi-pause' : 'mdi-play'"
            @click="emit('toggleAutoplay')"
          >
            {{ autoplay ? 'Pause autoplay' : 'Start autoplay' }}
          </v-btn>
        </v-col>
      </v-row>

      <v-slider
        :max="maxGeneration"
        :model-value="generationIndex"
        :min="0"
        :step="1"
        thumb-label="always"
        @update:model-value="onSeek"
      >
        <template #prepend>
          <span class="text-caption">Gen 0</span>
        </template>
        <template #append>
          <span class="text-caption">Gen {{ maxGeneration }}</span>
        </template>
      </v-slider>
    </template>
  </v-card>
</template>
