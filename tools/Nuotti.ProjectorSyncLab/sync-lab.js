const ignoreThreshold = 50;
const snapThreshold = 150;
const convergenceWindow = 1000;
const songStartOffset = 1000;
const lyrics = [
  [0, "Count-in"],
  [1500, "Every light follows one timeline"],
  [4000, "Sparse anchors, smooth pictures"],
  [7000, "The band keeps the clock"]
];

class Timeline {
  basePosition = 0;
  baseTime = 0;
  rate = 1;
  correction = 0;
  correctionAt = 0;
  ready = false;

  positionAt(now) {
    if (!this.ready) return 0;
    const progress = Math.max(0, Math.min(1, (now - this.correctionAt) / convergenceWindow));
    return this.basePosition + Math.max(0, now - this.baseTime) * this.rate + this.correction * progress;
  }

  apply(position, now) {
    if (!this.ready) {
      this.snap(position, now);
      return { mode: "snap", error: 0 };
    }

    const predicted = this.positionAt(now);
    const error = position - predicted;
    const magnitude = Math.abs(error);
    if (magnitude <= ignoreThreshold) return { mode: "ignore", error };
    if (magnitude <= snapThreshold) {
      this.basePosition = predicted;
      this.baseTime = now;
      this.correction = error;
      this.correctionAt = now;
      return { mode: "gradual", error };
    }

    this.snap(position, now);
    return { mode: "snap", error };
  }

  snap(position, now) {
    this.ready = true;
    this.basePosition = position;
    this.baseTime = now;
    this.correction = 0;
    this.correctionAt = now;
  }
}

const timeline = new Timeline();
let run = null;
let firstPlayingAnchor = true;
let steadySamples = [];
let correctionProbe = null;
let latestTruth = null;
let visualStartMeasured = false;
const byId = id => document.getElementById(id);
const ms = value => `${value >= 0 ? "+" : ""}${value.toFixed(1)} ms`;
const localMonotonicFromUtc = utc => performance.now() + (Date.parse(utc) - Date.now());
const log = message => {
  const row = document.createElement("div");
  row.textContent = message;
  byId("log").prepend(row);
};

byId("start").addEventListener("click", async () => {
  const tappedUtc = Date.now();
  const response = await fetch("/api/start", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ leadMs: 750 })
  });
  run = { ...(await response.json()), tappedUtc };
  firstPlayingAnchor = true;
  steadySamples = [];
  correctionProbe = null;
  latestTruth = null;
  visualStartMeasured = false;
  timeline.ready = false;
  byId("startError").textContent = "waiting for measured anchor";
  byId("lead").textContent = "waiting for measured anchor";
  log(`Engine accepted start; planned lead ${run.leadMs} ms`);
});

document.querySelectorAll("button[data-error]").forEach(button => button.addEventListener("click", async () => {
  if (!run) return log("Run the planned start first.");
  const error = Number(button.dataset.error);
  correctionProbe = { requestedAt: performance.now(), mode: null, evaluateAt: null };
  try {
    const response = await fetch(`/api/drift/${error}`, { method: "POST" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    log(`Engine will set its next measured anchor bias to ${ms(error)}`);
  } catch (error) {
    correctionProbe = null;
    log(`Could not inject drift: ${error.message}`);
  }
}));

new EventSource("/api/anchors").onmessage = event => {
  const receivedAt = performance.now();
  const anchor = JSON.parse(event.data);
  if (!run || anchor.PlaybackInstanceId !== run.playbackInstanceId) return;

  if (anchor.State === "Scheduled") {
    timeline.snap(0, localMonotonicFromUtc(anchor.BackendUtcCorrelation));
    log(`planned anchor #${anchor.Sequence} scheduled the browser timeline`);
    return;
  }

  if (anchor.State !== "Playing") return;

  const anchorAtLocalTime = localMonotonicFromUtc(anchor.BackendUtcCorrelation);
  const authoritativeAtReceipt = anchor.Frame / anchor.SampleRate * 1000
    + Math.max(0, receivedAt - anchorAtLocalTime) * anchor.Rate;
  latestTruth = { position: authoritativeAtReceipt, receivedAt, rate: anchor.Rate };
  const result = timeline.apply(authoritativeAtReceipt, receivedAt);

  if (firstPlayingAnchor) {
    firstPlayingAnchor = false;
    const measuredStartUtc = Date.parse(anchor.BackendUtcCorrelation) - anchor.Frame / anchor.SampleRate * 1000;
    const targetError = measuredStartUtc - Date.parse(run.plannedStartUtc);
    const tapToStart = measuredStartUtc - run.tappedUtc;
    byId("startError").textContent = `${ms(targetError)} target · ${tapToStart.toFixed(1)} ms tap`;
    byId("lead").textContent = tapToStart < 1250
      ? "Pass browser/Engine <1.25 s"
      : "Fail start targets";
  }

  steadySamples.push(Math.abs(result.error));
  if (steadySamples.length > 120) steadySamples.shift();
  const ordered = [...steadySamples].sort((a, b) => a - b);
  const p95 = ordered[Math.floor((ordered.length - 1) * .95)] ?? 0;
  byId("steadyError").textContent = `${p95.toFixed(1)} ms ${p95 <= 100 ? "· pass" : "· fail"}`;

  if (correctionProbe && correctionProbe.mode === null && result.mode !== "ignore") {
    correctionProbe.mode = result.mode;
    correctionProbe.evaluateAt = receivedAt + (result.mode === "gradual" ? convergenceWindow : 0);
  }
  byId("correction").textContent = `${result.mode} (${ms(result.error)})`;
  log(`measured anchor #${anchor.Sequence}: ${ms(result.error)} → ${result.mode}`);
};

function render(now) {
  const position = timeline.positionAt(now);
  if (run && !visualStartMeasured && position > 0) {
    visualStartMeasured = true;
    const visualError = Date.now() - Date.parse(run.plannedStartUtc);
    log(`first browser visual frame: ${ms(visualError)} from planned start`);
  }

  if (correctionProbe?.evaluateAt != null && now >= correctionProbe.evaluateAt && latestTruth) {
    const truthNow = latestTruth.position + Math.max(0, now - latestTruth.receivedAt) * latestTruth.rate;
    const residual = Math.abs(position - truthNow);
    const recovery = now - correctionProbe.requestedAt;
    byId("correction").textContent = `${correctionProbe.mode} · ${residual.toFixed(1)} ms after ${recovery.toFixed(0)} ms ${residual <= 150 ? "· pass" : "· fail"}`;
    correctionProbe = null;
  }

  const lyricTime = position - songStartOffset;
  const active = [...lyrics].reverse().find(([at]) => at <= lyricTime);
  byId("lyric").textContent = active?.[1] ?? (run ? "Count-in…" : "Ready for a planned start");
  byId("position").textContent = `${(position / 1000).toFixed(3)} s · LRC offset +${songStartOffset} ms`;
  requestAnimationFrame(render);
}
requestAnimationFrame(render);
