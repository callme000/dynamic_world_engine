#!/usr/bin/env python3
"""
Dynamic World Engine (DWE) - Automated Architecture & God Object Audit Scanner
Scans C# source code in src/ for architectural smells, line counts, and layer violations.
"""

import os
import re
import sys
from pathlib import Path
from datetime import datetime

# Configure utf-8 stdout for Windows consoles
if hasattr(sys.stdout, 'reconfigure'):
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

# Thresholds
WARN_LOC = 450
CRITICAL_LOC = 850

def analyze_csharp_files(src_dir):
    records = []
    
    for root, _, files in os.walk(src_dir):
        for file in files:
            if not file.endswith('.cs'):
                continue
            
            full_path = os.path.join(root, file)
            rel_path = os.path.relpath(full_path, src_dir).replace('\\', '/')
            
            with open(full_path, 'r', encoding='utf-8', errors='ignore') as f:
                lines = f.readlines()
            
            loc = len(lines)
            content = "".join(lines)
            
            # Count methods roughly
            method_matches = re.findall(r'(public|private|protected|internal)\s+(static\s+)?(async\s+)?[\w\<\>\[\]\,\s]+\s+(\w+)\s*\([^\)]*\)\s*\{', content)
            method_count = len(method_matches)
            
            # Layer smells detection
            smells = []
            
            # File I/O outside Database or Bridge
            if not rel_path.startswith(('Database/', 'Bridge/', 'Services/JsonRepair')):
                if 'File.Write' in content or 'File.Append' in content or 'StreamWriter' in content:
                    smells.append("Direct File I/O outside persistence/bridge layer")
            
            # Database mutation inside Parsing
            if rel_path.startswith('Commands/'):
                if '_store.Save' in content or '_store.AddEntity' in content or '_store.RemoveEntity' in content:
                    smells.append("Database state mutation/commit inside parser")
            
            # Native GTA Entity Creation outside Spawner/Script
            if not rel_path.startswith(('DynamicWorldEngineScript', 'Spatial/InteriorActivationJob', 'Runtime/EntitySpawnerService')):
                if 'World.CreateProp' in content or 'World.CreatePed' in content or 'World.CreateVehicle' in content:
                    smells.append("Direct native World.Create* call in non-spawner class")
            
            # Multi-system coupling in Spatial
            if rel_path.startswith('Spatial/'):
                if 'LivingLsAiBridge' in content:
                    smells.append("Narrative AI file-bridge coupling in spatial planner")
                if 'World.CreateBlip' in content and not rel_path.startswith('Spatial/BlipManager'):
                    smells.append("Direct Blip creation bypassing BlipManager")
            
            # Determine status
            if loc >= CRITICAL_LOC or len(smells) >= 2:
                status = "🚨 GOD OBJECT / ENTANGLED"
            elif loc >= WARN_LOC or len(smells) == 1:
                status = "🟡 WATCHLIST / ELEVATED"
            else:
                status = "🟢 CLEAN"
                
            records.append({
                "rel_path": rel_path,
                "loc": loc,
                "methods": method_count,
                "smells": smells,
                "status": status
            })
            
    # Sort descending by LOC
    records.sort(key=lambda x: x["loc"], reverse=True)
    return records

def generate_markdown_report(records):
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    total_loc = sum(r["loc"] for r in records)
    total_files = len(records)
    god_objects = [r for r in records if "🚨" in r["status"]]
    watchlist = [r for r in records if "🟡" in r["status"]]
    clean = [r for r in records if "🟢" in r["status"]]
    
    lines = []
    lines.append(f"### Automated Codebase Audit Scan ({timestamp})")
    lines.append(f"**Total C# Source Files:** {total_files} | **Total LOC:** {total_loc:,}")
    lines.append(f"**Classification:** 🚨 {len(god_objects)} God Objects | 🟡 {len(watchlist)} Elevated/Watchlist | 🟢 {len(clean)} Clean\n")
    
    lines.append("| Status | Class / File | Lines | Methods | Architectural Smells Detected |")
    lines.append("| :--- | :--- | :---: | :---: | :--- |")
    
    for r in records:
        smell_str = "<br>• ".join(r["smells"])
        if smell_str:
            smell_str = "• " + smell_str
        else:
            smell_str = "None (Well-bounded)"
            
        lines.append(f"| {r['status']} | `{r['rel_path']}` | {r['loc']:,} | {r['methods']} | {smell_str} |")
        
    return "\n".join(lines)

def main():
    script_dir = Path(__file__).resolve().parent
    base_dir = script_dir.parent
    src_dir = base_dir / "src"
    
    if not src_dir.exists():
        print(f"Error: {src_dir} does not exist.")
        sys.exit(1)
        
    records = analyze_csharp_files(str(src_dir))
    report = generate_markdown_report(records)
    
    # Print summary to stdout
    print(f"\n=======================================================")
    print(f"  Dynamic World Engine — Architecture & God Object Audit")
    print(f"=======================================================")
    print(f"Scanned {len(records)} C# files in src/\n")
    
    for r in records[:15]:
        status_icon = "🚨" if "🚨" in r["status"] else ("🟡" if "🟡" in r["status"] else "🟢")
        smell_alert = f" -> [{', '.join(r['smells'])}]" if r['smells'] else ""
        print(f" {status_icon} {r['rel_path']:<45} | {r['loc']:>5} lines | {r['methods']:>3} methods{smell_alert}")
        
    print("\nRun complete. Full markdown table generated.")
    
    # Check if --output-md is passed
    if "--output-md" in sys.argv:
        out_file = base_dir / "AUDIT_SCAN_LATEST.md"
        with open(out_file, "w", encoding="utf-8") as f:
            f.write(report)
        print(f"Saved latest scan report to: {out_file}")

if __name__ == "__main__":
    main()
