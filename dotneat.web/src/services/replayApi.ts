import type { ExperimentRun, Generation } from '@/types/replay'

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) {
    throw new Error(`Request failed (${response.status}) for ${url}`)
  }

  return (await response.json()) as T
}

export function fetchRuns(): Promise<ExperimentRun[]> {
  return fetchJson<ExperimentRun[]>('/api/runs')
}

export function fetchGenerations(runId: string): Promise<Generation[]> {
  return fetchJson<Generation[]>(`/api/runs/${encodeURIComponent(runId)}/generations`)
}
