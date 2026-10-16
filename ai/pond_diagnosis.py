"""Dependency-free Bayesian diagnosis for simple pond observations.

Run with JSON on stdin, for example:
    echo '{"water_quality":0.3,"debris":0.8,"wildlife_activity":0.4}' | python ai/pond_diagnosis.py
"""

import json
import math
import sys


HYPOTHESES = {
    "Pond pollution": (0.50, (0.90, 0.82, 0.70, 0.48)),
    "Habitat disturbance": (0.30, (0.48, 0.78, 0.86, 0.82)),
    "Vegetation loss": (0.20, (0.38, 0.46, 0.62, 0.94)),
}


def diagnose(observations):
    """Return the most likely environmental problem and its posterior confidence."""
    water_quality = _unit(observations.get("water_quality", 0.5))
    debris = _unit(observations.get("debris", 0.0))
    wildlife_activity = _unit(observations.get("wildlife_activity", 0.5))
    vegetation_health = _unit(observations.get("vegetation_health", 0.5))
    evidence = (1.0 - water_quality, debris, 1.0 - wildlife_activity, 1.0 - vegetation_health)

    log_scores = {}
    for name, (prior, likelihoods) in HYPOTHESES.items():
        score = math.log(prior)
        for observed, likelihood in zip(evidence, likelihoods):
            likelihood = min(max(likelihood, 1e-6), 1.0 - 1e-6)
            score += observed * math.log(likelihood) + (1.0 - observed) * math.log(1.0 - likelihood)
        log_scores[name] = score

    peak = max(log_scores.values())
    weights = {name: math.exp(score - peak) for name, score in log_scores.items()}
    total = sum(weights.values())
    problem = max(weights, key=weights.get)
    return {"problem": problem, "confidence": round(weights[problem] / total, 3)}


def _unit(value):
    value = float(value)
    if not math.isfinite(value):
        raise ValueError("observation values must be finite numbers")
    return min(max(value, 0.0), 1.0)


if __name__ == "__main__":
    try:
        result = diagnose(json.load(sys.stdin))
        print(json.dumps(result))
    except (ValueError, TypeError, json.JSONDecodeError) as error:
        print(json.dumps({"error": str(error)}), file=sys.stderr)
        sys.exit(2)
