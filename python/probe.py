"""Local system sampler. Communicates with the C# service using one JSON line per request."""
import json
import platform
import sys
import time

try:
    import psutil
except ImportError as exc:
    print(json.dumps({"error": "Install Python dependencies with: python -m pip install -r python/requirements.txt"}), flush=True)
    raise SystemExit(1) from exc

psutil.cpu_percent(interval=None)
previous = {"net": psutil.net_io_counters(), "disk": psutil.disk_io_counters(), "at": time.monotonic()}

def snapshot():
    now = time.monotonic()
    elapsed = max(now - previous["at"], 0.001)
    net, disk = psutil.net_io_counters(), psutil.disk_io_counters()
    cpu = psutil.cpu_percent(interval=0.15)
    memory = psutil.virtual_memory()
    swap = psutil.swap_memory()
    drives = []
    for part in psutil.disk_partitions(all=False):
        if "cdrom" in part.opts or not part.fstype:
            continue
        try:
            usage = psutil.disk_usage(part.mountpoint)
            drives.append({"mount": part.mountpoint, "total": usage.total, "used": usage.used,
                           "free": usage.free, "percent": usage.percent, "filesystem": part.fstype})
        except OSError:
            pass
    result = {
        "timestamp": time.time(), "platformName": platform.system(), "cpuPercent": cpu, "cpuCores": psutil.cpu_percent(interval=None, percpu=True),
        "memoryTotal": memory.total, "memoryUsed": memory.used, "memoryPercent": memory.percent,
        "swapTotal": swap.total, "swapUsed": swap.used, "swapPercent": swap.percent,
        "networkDown": max(0, net.bytes_recv - previous["net"].bytes_recv) / elapsed,
        "networkUp": max(0, net.bytes_sent - previous["net"].bytes_sent) / elapsed,
        "diskRead": max(0, disk.read_bytes - previous["disk"].read_bytes) / elapsed if disk and previous["disk"] else 0,
        "diskWrite": max(0, disk.write_bytes - previous["disk"].write_bytes) / elapsed if disk and previous["disk"] else 0,
        "drives": drives, "processCount": len(psutil.pids()), "uptimeSeconds": max(0, time.time() - psutil.boot_time())
    }
    previous.update({"net": net, "disk": disk, "at": now})
    return result

def diagnostics():
    data = snapshot()
    checks = []
    def add(level, title, detail): checks.append({"level": level, "title": title, "detail": detail})
    if data["memoryPercent"] >= 90:
        add("warning", "Memory pressure", f"{data['memoryPercent']:.0f}% of RAM is in use. Check the Processes page for unusually large applications.")
    if data["cpuPercent"] >= 90:
        add("warning", "High CPU activity", f"CPU usage is {data['cpuPercent']:.0f}%. Check Task Manager for a busy process and allow updates or scans to finish.")
    for drive in data["drives"]:
        if drive["total"] >= 1024 ** 3 and drive["percent"] >= 90:
            add("warning", f"Low space on {drive['mount']}", f"{drive['free'] / (1024**3):.1f} GB free. Review large files before removing anything.")
    if not checks: add("good", "No current resource warnings", "CPU, memory, and mounted drive space look within normal ranges.")
    return {"checks": checks, "checkedAt": time.time()}

for line in sys.stdin:
    try:
        action = line.strip()
        if action == "snapshot": response = snapshot()
        elif action == "diagnostics": response = diagnostics()
        elif action == "report": response = {"generatedAt": time.time(), "snapshot": snapshot(), "diagnostics": diagnostics()["checks"]}
        else: response = {"error": "Unknown probe action."}
        print(json.dumps(response, separators=(",", ":")), flush=True)
    except Exception as exc:
        print(json.dumps({"error": str(exc)}), flush=True)
