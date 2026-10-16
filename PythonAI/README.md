# ForestGuardian Python AI

These are deterministic, standard-library Python algorithms for actual field problems in the game. Each script accepts an input JSON file and an optional output JSON file:

```text
python PythonAI/bayesian.py PythonAI/samples/bayesian_input.json PythonAI/samples/bayesian_output.json
```

Without file arguments, a script reads one JSON object from stdin and prints its JSON result to stdout. Invalid input exits with code 2 and a JSON error on stderr. The sample output files are checked-in examples; rerun the commands above to regenerate them.

## Algorithms and game data

| Script | ForestGuardian problem | Input | Output | Unity connection |
| --- | --- | --- | --- | --- |
| `bfs.py` | Find the nearest reachable spring or pond in the discovered trail graph. Equal-cost edges make BFS appropriate. | `start`, adjacency `graph`, and candidate `targets`. Neighbors can be node IDs or `{ "to": id }`. | Chosen `target`, shortest-hop `path`, `distance`, and visited nodes. | Intended for selecting the next reachable water location; not yet used by live Unity navigation. |
| `dfs.py` | Discover all connected habitat nodes in the region around a clue or pond. | `start`, adjacency `graph`, optional region label. | Reachable `nodes`, traversal order, and component `count`. | Intended for mapping a connected habitat region; currently a standalone analysis tool. |
| `astar.py` | Route from shelter to a field site while avoiding costly steep or obstructed terrain. | `start`, `goal`, adjacency edges with `cost`; optional `positions` for Euclidean heuristic or explicit `heuristic` values. | `path`, accumulated terrain `cost`, reachability, and expanded nodes. | Intended for Unity route guidance; the current compass/map still use the existing Unity map data. |
| `bayesian.py` | Diagnose contamination, drought, disease, or habitat disturbance from field evidence. | Normalized `water_clarity`, `vegetation_health`, `wildlife_activity`, `rainfall`, and `dead_vegetation`, each in `[0,1]`. | Most likely `diagnosis`, posterior `confidence`, all cause `probabilities`, and evidence/model labels. | **Used in the Eastern Pond investigation.** `PythonAIBridge` writes the evidence JSON, launches this script only after the player presses E to submit the water test, reads the output JSON, and advances the quest only after a valid result. |
| `csp.py` | Place filters, saplings, and wildlife shelters at viable restoration sites without assigning two resources to one site or violating site conflicts. | `resources` with type and requirements; `locations` with suitability, soil, water distance, restriction, and optional `conflicts_with`. | Whether assignment succeeded, resource-to-site assignments, attempts, and candidate sites. | Intended for planning the restoration kit; currently a standalone data analysis tool. |
| `logic.py` | Infer a water-related disturbance from several independent clues, then infer likely shoreline pollution from supporting evidence. | Initial string `facts` and rules with `if_all` premises and one `then` conclusion. Rules chain until no new facts are inferred. | Full facts, each fired rule with its premises, and whether a water disturbance was inferred. | Intended for clue-board/journal reasoning; currently a standalone data analysis tool. |
| `planning.py` | Find a valid sequence of restoration actions from investigation to monitored recovery. | Initial facts, goal facts, and actions with `preconditions`, `add`, and optional `remove` effects. | A shortest action `plan`, success flag, goal, and final facts. | Intended for chapter objective planning; currently a standalone data analysis tool. |

## Unity ↔ Python exchange

Unity does not keep a Python server running. The Eastern Pond water-test interaction is the explicit trigger. `Assets/Scripts/Systems/PythonAIBridge.cs` creates `pond_input.json` under `Application.persistentDataPath/ForestGuardian/PythonAI`, invokes the known `PythonAI/bayesian.py` entry point with those file paths, then parses `pond_output.json`. The interpreter is `python` on Windows and `python3` on other platforms. The process has a 10-second timeout. Missing Python, missing scripts, malformed output, and process failures are reported to the player and leave the quest ready to retry; Unity does not label the C# fallback as Python.

The project-root `PythonAI` folder supports editor use. For a standalone build, package the same folder at `Assets/StreamingAssets/PythonAI` and install Python on the target machine. The bridge checks both locations. The other scripts share the same file contract and can be connected through the same explicit, JSON-based trigger pattern without changing their algorithm inputs or outputs.

Bayesian likelihoods are demonstration assumptions, not calibrated ecological estimates. The pond interaction uses live ecosystem water and vegetation values, counts nearby animals for activity, and supplies current weather as a rainfall proxy. The game displays the model's posterior distribution so the diagnosis can be explained in a presentation.
