# dotneat.web

Vue + Vuetify replay app for `experiments.db`.

## What it does

- Loads experiment runs from SQLite (`ExperimentRuns` + `Generations`)
- Lets learners filter/sort runs and pick one for replay
- Replays generations with slider, previous/next, and autoplay
- Shows generation metrics and champion genome topology
- Includes teaching-focused guidance about what changed and why it matters

If `experiments.db` is missing, the dev API runs `DotNeat.Runner` (`xor`) once to generate data.

## Local development

```sh
npm install
npm run dev
```

Then open `http://localhost:56367`.

## Quality checks

```sh
npm run lint
npm run build
```
