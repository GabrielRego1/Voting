#!/usr/bin/env python3
"""Samples CPU and memory of the API, the load generator and the PostgreSQL container once per second.

CPU is reported as percent of one core (400 = four cores busy). Sampling stops when the generator exits.
Output CSV columns: t, api_cpu, api_rss_mb, gen_cpu, gen_rss_mb, pg_cpu, pg_mem_mb.
"""

import argparse
import csv
import os
import time

CLK_TCK = os.sysconf("SC_CLK_TCK")


def proc_cpu_seconds(pid):
    with open(f"/proc/{pid}/stat", encoding="ascii") as f:
        fields = f.read().rsplit(")", 1)[1].split()
    # fields[0] is state (3rd field of stat); utime and stime are the 14th and 15th fields.
    return (int(fields[11]) + int(fields[12])) / CLK_TCK


def proc_rss_mb(pid):
    with open(f"/proc/{pid}/status", encoding="ascii") as f:
        for line in f:
            if line.startswith("VmRSS:"):
                return int(line.split()[1]) / 1024
    return 0.0


def cgroup_dir(pid):
    with open(f"/proc/{pid}/cgroup", encoding="ascii") as f:
        for line in f:
            if line.startswith("0::"):
                return "/sys/fs/cgroup" + line.strip()[3:]
    raise RuntimeError(f"cgroup v2 path not found for pid {pid}")


def cgroup_cpu_seconds(path):
    with open(os.path.join(path, "cpu.stat"), encoding="ascii") as f:
        for line in f:
            key, value = line.split()
            if key == "usage_usec":
                return int(value) / 1_000_000
    return 0.0


def cgroup_mem_mb(path):
    with open(os.path.join(path, "memory.current"), encoding="ascii") as f:
        return int(f.read()) / (1024 * 1024)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--api-pid", type=int, required=True)
    parser.add_argument("--gen-pid", type=int, required=True)
    parser.add_argument("--pg-pid", type=int, required=True, help="host pid of any process inside the postgres container")
    parser.add_argument("--out", required=True)
    parser.add_argument("--interval", type=float, default=1.0)
    args = parser.parse_args()

    pg_cgroup = cgroup_dir(args.pg_pid)
    start = time.monotonic()
    prev_time = start
    prev = (proc_cpu_seconds(args.api_pid), proc_cpu_seconds(args.gen_pid), cgroup_cpu_seconds(pg_cgroup))

    with open(args.out, "w", newline="", encoding="ascii") as out:
        writer = csv.writer(out)
        writer.writerow(["t", "api_cpu", "api_rss_mb", "gen_cpu", "gen_rss_mb", "pg_cpu", "pg_mem_mb"])
        while True:
            time.sleep(args.interval)
            try:
                now = time.monotonic()
                cur = (proc_cpu_seconds(args.api_pid), proc_cpu_seconds(args.gen_pid), cgroup_cpu_seconds(pg_cgroup))
                gen_rss = proc_rss_mb(args.gen_pid)
            except (FileNotFoundError, ProcessLookupError):
                break  # generator (or API) exited
            dt = now - prev_time
            cpu = [round(100 * (c - p) / dt, 1) for c, p in zip(cur, prev)]
            writer.writerow([
                round(now - start, 1),
                cpu[0], round(proc_rss_mb(args.api_pid), 1),
                cpu[1], round(gen_rss, 1),
                cpu[2], round(cgroup_mem_mb(pg_cgroup), 1),
            ])
            out.flush()
            prev, prev_time = cur, now


if __name__ == "__main__":
    main()
