"""Forward-chaining production rules for explainable environmental clue inference."""
from io_utils import run


def infer(data):
    facts = set(data.get("facts", []))
    rules = data.get("rules", [])
    fired, changed = [], True
    while changed:
        changed = False
        for index, rule in enumerate(rules):
            requirements = set(rule.get("if_all", []))
            if requirements.issubset(facts) and rule["then"] not in facts:
                facts.add(rule["then"])
                fired.append({"rule": rule.get("name", f"rule_{index + 1}"), "from": sorted(requirements), "inferred": rule["then"]})
                changed = True
    return {"facts": sorted(facts), "inferences": fired, "suspected_disturbance": "water_related_ecosystem_disturbance" in facts}


if __name__ == "__main__":
    run(infer)
