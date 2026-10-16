"""Explainable Naive Bayes diagnosis from field observations (all inputs 0..1)."""
import math
from io_utils import run

CAUSES = {
    "contamination": (0.34, (0.91, 0.57, 0.67, 0.20, 0.46)),
    "drought": (0.26, (0.56, 0.77, 0.54, 0.89, 0.63)),
    "disease": (0.20, (0.42, 0.83, 0.78, 0.18, 0.91)),
    "habitat_disturbance": (0.20, (0.55, 0.69, 0.88, 0.32, 0.67)),
}


def diagnose(data):
    observed = [1 - _unit(data.get("water_clarity", 0.5)), 1 - _unit(data.get("vegetation_health", 0.5)),
                1 - _unit(data.get("wildlife_activity", 0.5)), _unit(data.get("rainfall", 0.5)),
                _unit(data.get("dead_vegetation", 0.0))]
    log_scores = {}
    for cause, (prior, likelihoods) in CAUSES.items():
        score = math.log(prior)
        for value, likelihood in zip(observed, likelihoods):
            p = min(max(likelihood, 1e-9), 1 - 1e-9)
            score += value * math.log(p) + (1 - value) * math.log(1 - p)
        log_scores[cause] = score
    peak = max(log_scores.values())
    weights = {key: math.exp(value - peak) for key, value in log_scores.items()}
    total = sum(weights.values())
    probabilities = {key: round(value / total, 6) for key, value in weights.items()}
    diagnosis = max(probabilities, key=probabilities.get)
    return {"diagnosis": diagnosis, "confidence": probabilities[diagnosis], "probabilities": probabilities,
            "evidence_used": ["water_clarity", "vegetation_health", "wildlife_activity", "rainfall", "dead_vegetation"],
            "model": "naive_bayes_independent_evidence"}


def _unit(value):
    value = float(value)
    if not math.isfinite(value):
        raise ValueError("evidence values must be finite")
    return min(1.0, max(0.0, value))


if __name__ == "__main__":
    run(diagnose)
