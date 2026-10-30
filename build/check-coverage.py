"""Fails when any Cobertura report under the given directory is below the required coverage."""
import glob
import sys
import xml.etree.ElementTree as ET

results_directory = sys.argv[1]
required = float(sys.argv[2]) if len(sys.argv) > 2 else 1.0
reports = glob.glob(f"{results_directory}/**/coverage.cobertura.xml", recursive=True)
if not reports:
    sys.exit(f"No coverage reports found under {results_directory}.")

failed = False
for report in reports:
    root = ET.parse(report).getroot()
    line_rate = float(root.get("line-rate"))
    branch_rate = float(root.get("branch-rate"))
    print(f"{report}: line {line_rate:.2%}, branch {branch_rate:.2%}")
    if line_rate < required or branch_rate < required:
        failed = True
        for cls in root.iter("class"):
            for line in cls.iter("line"):
                condition = line.get("condition-coverage", "")
                if line.get("hits") == "0" or (condition and not condition.startswith("100%")):
                    print(f"  not covered: {cls.get('filename')}:{line.get('number')} {condition}")

if failed:
    sys.exit(f"Coverage is below the required {required:.0%}.")
