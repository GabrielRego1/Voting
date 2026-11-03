#!/usr/bin/env bash
# Stress ramp for the Voting API: runs the load generator at increasing rates (one fresh poll per step),
# pins API, PostgreSQL and generator to separate CPUs, samples their resource usage and stops at the first
# failing step. Optionally refines the limit by binary search and confirms it with extra runs.
# Raw output goes to benchmarks/$BENCH_VERSION/raw; summarize.py turns it into summary.json and README.md.
#
# WARNING: recreates the local PostgreSQL container and its volume (the local dev database is erased).
set -euo pipefail

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$root"

BENCH_VERSION=${BENCH_VERSION:-v0}
RATES=${RATES:-"10000 15000 20000 25000 30000 40000 50000 60000"}
DURATION=${DURATION:-30}
CONNECTIONS=${CONNECTIONS:-1024}
DISTRIBUTION=${DISTRIBUTION:-50,30,20}
RETRY_FRACTION=${RETRY_FRACTION:-0.05}
RESULT_TIMEOUT=${RESULT_TIMEOUT:-15}
API_CPUS=${API_CPUS:-0-7}
PG_CPUS=${PG_CPUS:-8-11}
GEN_CPUS=${GEN_CPUS:-12-15}
REFINE=${REFINE:-1}
REFINE_STEP=${REFINE_STEP:-1000}
CONFIRM_RUNS=${CONFIRM_RUNS:-2}
WARMUP_RATE=${WARMUP_RATE:-2000}
WARMUP_DURATION=${WARMUP_DURATION:-10}
PAUSE=${PAUSE:-5}
MIN_ACHIEVED=${MIN_ACHIEVED:-0.98}
FORCE=${FORCE:-0}
BASE_URL=http://localhost:5080

API_BIN=src/Voting.Api/bin/Release/net10.0
WORKLOAD_DLL=$root/tools/Voting.Workload/bin/Release/net10.0/Voting.Workload.dll
out=$root/benchmarks/$BENCH_VERSION
raw=$out/raw

log() { printf '[stress %(%H:%M:%S)T] %s\n' -1 "$*" >&2; }
die() { log "error: $*"; exit 1; }

for cmd in dotnet docker jq python3 taskset curl; do
  command -v "$cmd" >/dev/null || die "$cmd not found"
done
docker info >/dev/null 2>&1 || die "Docker daemon is not running"
if curl -s -o /dev/null "$BASE_URL"; then die "something is already listening on $BASE_URL; stop it first"; fi
if [[ -e $raw && $FORCE != 1 ]]; then die "$raw already exists; use another BENCH_VERSION or FORCE=1"; fi
rm -rf "$raw"
mkdir -p "$raw"

log "building Release"
dotnet build Voting.slnx -c Release -v quiet -nologo >"$raw/build.log" 2>&1 || die "build failed; see $raw/build.log"

log "recreating PostgreSQL (local database is erased) pinned to CPUs $PG_CPUS"
docker compose down -v >/dev/null 2>&1
docker compose up -d --wait postgres >/dev/null 2>&1
pg_container=$(docker compose ps -q postgres)
docker update --cpuset-cpus "$PG_CPUS" "$pg_container" >/dev/null
pg_pid=$(docker inspect -f '{{.State.Pid}}' "$pg_container")

log "starting API pinned to CPUs $API_CPUS"
(cd "$API_BIN" && ASPNETCORE_ENVIRONMENT=Development exec taskset -c "$API_CPUS" dotnet Voting.Api.dll) >"$raw/api.log" 2>&1 &
api_pid=$!
trap 'kill $api_pid 2>/dev/null || true; wait $api_pid 2>/dev/null || true' EXIT
curl -s -o /dev/null --retry-connrefused --retry 30 --retry-delay 1 \
  "$BASE_URL/polls/00000000-0000-0000-0000-000000000000" || die "API did not start; see $raw/api.log"

# Power state matters on laptops: battery or power-saver profiles cap the clock and skew results.
on_ac=false
for supply in /sys/class/power_supply/*; do
  if [[ $(cat "$supply/type" 2>/dev/null) == Mains && $(cat "$supply/online" 2>/dev/null) == 1 ]]; then on_ac=true; fi
done
[[ -z $(ls /sys/class/power_supply 2>/dev/null) ]] && on_ac=true  # desktop/server without power supply info
power_profile=$(powerprofilesctl get 2>/dev/null || echo unknown)
governor=$(cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor 2>/dev/null || echo unknown)
[[ $on_ac == true ]] || log "WARNING: running on battery; results are not comparable with runs on AC power"
[[ $power_profile == power-saver ]] && log "WARNING: power profile is power-saver; prefer 'powerprofilesctl set performance'"

jq -n \
  --argjson onAc "$on_ac" --arg powerProfile "$power_profile" --arg governor "$governor" \
  --arg version "$BENCH_VERSION" \
  --arg commit "$(git rev-parse HEAD)" \
  --arg dirty "$(git status --porcelain -- src tools/Voting.Workload | head -1)" \
  --arg date "$(date -Iseconds)" \
  --arg cpu "$(lscpu | sed -n 's/^Model name: *//p')" \
  --argjson logicalCpus "$(nproc)" \
  --arg memory "$(free -h | awk '/^Mem:/ {print $2}')" \
  --arg kernel "$(uname -r)" \
  --arg dotnet "$(dotnet --version)" \
  --arg postgres "$(docker exec "$pg_container" postgres --version)" \
  --arg apiCpus "$API_CPUS" --arg pgCpus "$PG_CPUS" --arg genCpus "$GEN_CPUS" \
  --argjson duration "$DURATION" --argjson connections "$CONNECTIONS" \
  --arg distribution "$DISTRIBUTION" --argjson retryFraction "$RETRY_FRACTION" \
  --argjson minAchieved "$MIN_ACHIEVED" --arg rates "$RATES" \
  --argjson refine "$REFINE" --argjson refineStep "$REFINE_STEP" --argjson confirmRuns "$CONFIRM_RUNS" \
  '{version: $version, commit: $commit, dirtyTree: ($dirty != ""), date: $date,
    machine: {cpu: $cpu, logicalCpus: $logicalCpus, memory: $memory, kernel: $kernel,
             power: {onAc: $onAc, profile: $powerProfile, governor: $governor}},
    software: {dotnet: $dotnet, postgres: $postgres},
    cpuPinning: {api: $apiCpus, postgres: $pgCpus, generator: $genCpus},
    parameters: {durationSeconds: $duration, connections: $connections, distribution: $distribution,
                 retryFraction: $retryFraction, minAchievedFraction: $minAchieved,
                 rampRates: ($rates | split(" ") | map(select(. != "") | tonumber)),
                 refine: ($refine == 1), refineStep: $refineStep, confirmRuns: $confirmRuns}}' \
  >"$raw/environment.json"

# Only one poll may be open at a time: waits until the previous step's poll is finalized (result available).
wait_previous_poll() {
  local previous=$1 poll_id
  [[ -f $raw/$previous.json ]] || return 0
  poll_id=$(jq -r '.pollId // empty' "$raw/$previous.json" 2>/dev/null) || return 0
  [[ -n $poll_id ]] || return 0
  for _ in $(seq 1 90); do
    [[ $(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/polls/$poll_id/result") == 200 ]] && return 0
    sleep 1
  done
  die "poll $poll_id of step $previous was not finalized after 90 s"
}

PREVIOUS_TAG=
# Runs one generator execution. Args: tag phase rate duration. Sets STEP_PASSED to true or false.
run_step() {
  local tag=$1 phase=$2 rate=$3 duration=$4 code=0
  # The poll closes 30 s after opening; wait long enough for its result even on short steps.
  local result_timeout=$(( RESULT_TIMEOUT > 45 - duration ? RESULT_TIMEOUT : 45 - duration ))
  [[ -n $PREVIOUS_TAG ]] && wait_previous_poll "$PREVIOUS_TAG"
  PREVIOUS_TAG=$tag
  log "step $tag: $rate votes/s for $duration s ($phase)"
  taskset -c "$GEN_CPUS" dotnet "$WORKLOAD_DLL" \
    --base-url "$BASE_URL" --rate "$rate" --duration "$duration" --distribution "$DISTRIBUTION" \
    --retry-fraction "$RETRY_FRACTION" --connections "$CONNECTIONS" --result-timeout "$result_timeout" \
    --output json >"$raw/$tag.json" 2>"$raw/$tag.log" &
  local gen_pid=$!
  python3 -I tools/stress/sample.py --api-pid "$api_pid" --gen-pid "$gen_pid" --pg-pid "$pg_pid" \
    --out "$raw/$tag.resources.csv" &
  local sampler_pid=$!
  if wait "$gen_pid"; then code=0; else code=$?; fi
  wait "$sampler_pid" || true
  kill -0 "$api_pid" 2>/dev/null || die "API exited during step $tag; see $raw/api.log"

  local failures
  if [[ $code -le 1 ]] && jq -e . "$raw/$tag.json" >/dev/null 2>&1; then
    failures=$(jq -c --argjson rate "$rate" --argjson min "$MIN_ACHIEVED" '
      [ (if .achievedRate < $min * $rate then "achievedRate" else empty end),
        (.counts.rejected | to_entries[] | select(.key != "voting_closed" and .value > 0) | "rejected." + .key),
        (if .counts.transportErrors > 0 then "transportErrors" else empty end),
        (.checks | to_entries[] | select(.value != true) | "checks." + .key) ]' "$raw/$tag.json")
  else
    failures='["generatorError"]'
  fi
  local passed=false
  if [[ $failures == "[]" ]]; then passed=true; fi
  jq -nc --arg tag "$tag" --arg phase "$phase" --argjson rate "$rate" --argjson exitCode "$code" \
    --argjson failures "$failures" --argjson passed "$passed" \
    '{tag: $tag, phase: $phase, rate: $rate, exitCode: $exitCode, passed: $passed, failures: $failures}' \
    >>"$raw/steps.jsonl"
  log "step $tag: $([[ $passed == true ]] && echo PASS || echo "FAIL $failures")"
  sleep "$PAUSE"
  STEP_PASSED=$passed
}

run_step warmup warmup "$WARMUP_RATE" "$WARMUP_DURATION"

last_pass=0
first_fail=0
for rate in $RATES; do
  run_step "ramp-$rate" ramp "$rate" "$DURATION"
  if [[ $STEP_PASSED == true ]]; then
    last_pass=$rate
  else
    first_fail=$rate
    break
  fi
done

if [[ $REFINE == 1 && $last_pass -gt 0 && $first_fail -gt 0 ]]; then
  lo=$last_pass hi=$first_fail
  while (( hi - lo > REFINE_STEP )); do
    mid=$(( (lo + hi) / 2 / 500 * 500 ))
    (( mid <= lo )) && break
    run_step "refine-$mid" refine "$mid" "$DURATION"
    if [[ $STEP_PASSED == true ]]; then lo=$mid; else hi=$mid; fi
  done
  last_pass=$lo
fi

if (( last_pass > 0 )); then
  for i in $(seq 1 "$CONFIRM_RUNS"); do
    run_step "confirm-$last_pass-$i" confirm "$last_pass" "$DURATION"
  done
fi

log "done: highest passing rate $last_pass votes/s; first failing rate $first_fail votes/s"
python3 -I tools/stress/summarize.py "$out"
log "report: $out/README.md"
