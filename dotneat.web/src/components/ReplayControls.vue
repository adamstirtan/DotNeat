<script setup lang="ts">
defineProps<{
  generationIndex: number
  maxGeneration: number
}>()

const emit = defineEmits<{
  seek: [value: number]
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
  <v-card>
    <template #text>
      <div class="pt-5">
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
      </div>
    </template>
  </v-card>
</template>
