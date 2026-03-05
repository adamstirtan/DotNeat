import { resolve } from 'node:path'
import { existsSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import BetterSqlite3 from 'better-sqlite3'
import type { Connect } from 'vite'
import { defineConfig } from 'vite'
import plugin from '@vitejs/plugin-vue'
import vuetify from 'vite-plugin-vuetify'

const repositoryRoot = resolve(__dirname, '..')
const dbPath = resolve(repositoryRoot, 'experiments.db')
const runnerProjectPath = resolve(repositoryRoot, 'DotNeat.Runner', 'DotNeat.Runner.csproj')

function ensureDatabase(): void {
  if (existsSync(dbPath)) {
    return
  }

  const seed = Date.now()
  const result = spawnSync('dotnet', ['run', '--project', runnerProjectPath, '--', 'xor', `${seed}`], {
    cwd: repositoryRoot,
    encoding: 'utf-8',
  })

  if (result.status !== 0) {
    throw new Error(`Unable to generate experiments.db: ${result.stderr || result.stdout}`)
  }
}

function withDatabase<T>(operation: (db: InstanceType<typeof BetterSqlite3>) => T): T {
  ensureDatabase()
  const db = new BetterSqlite3(dbPath, { readonly: true })

  try {
    return operation(db)
  } finally {
    db.close()
  }
}

function createApiMiddleware(): Connect.NextHandleFunction {
  return (req, res, next) => {
    if (!req.url || !req.url.startsWith('/api/')) {
      next()
      return
    }

    try {
      const requestUrl = new URL(req.url, 'http://localhost')

      if (requestUrl.pathname === '/api/runs') {
        const runs = withDatabase((db) =>
          db
            .prepare(
              `SELECT RunId, ExperimentName, Seed, StartedUtc, FinishedUtc, BestFitness, Completed
               FROM ExperimentRuns
               ORDER BY StartedUtc DESC`,
            )
            .all(),
        )

        res.setHeader('Content-Type', 'application/json')
        res.end(JSON.stringify(runs))
        return
      }

      const generationMatch = requestUrl.pathname.match(/^\/api\/runs\/([^/]+)\/generations$/)
      if (generationMatch) {
        const runId = decodeURIComponent(generationMatch[1])
        const generations = withDatabase((db) =>
          db
            .prepare(
              `SELECT RunId, GenerationIndex, BestFitness, AverageFitness, SpeciesCount,
                      AverageComplexity, PopulationSize, BestGenomeJson
               FROM Generations
               WHERE RunId = ?
               ORDER BY GenerationIndex ASC`,
            )
            .all(runId),
        )

        res.setHeader('Content-Type', 'application/json')
        res.end(JSON.stringify(generations))
        return
      }

      res.statusCode = 404
      res.end('Not Found')
    } catch (error) {
      res.statusCode = 500
      res.setHeader('Content-Type', 'application/json')
      const message = error instanceof Error ? error.message : 'Unexpected API error'
      res.end(JSON.stringify({ error: message }))
    }
  }
}

const apiMiddleware = createApiMiddleware()

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    plugin(),
    vuetify({ autoImport: true }),
    {
      name: 'dotneat-replay-api',
      configureServer(server) {
        server.middlewares.use(apiMiddleware)
      },
      configurePreviewServer(server) {
        server.middlewares.use(apiMiddleware)
      },
    },
  ],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
    },
  },
  server: {
    port: 56367,
  },
})
