const state = {
  layer: "SurfaceWater",
  catalog: [],
  catalogById: new Map(),
  fluxCatalog: [],
  fluxById: new Map(),
  eventCatalog: [],
  eventByKind: new Map(),
  regimes: null,
  snapshot: null,
  summary: null,
  selected: null,
  selectedCell: null,
  explanation: null,
  worldHistory: null,
  cellHistory: null,
  historyResolution: "Recent",
  mapMode: "value",
  checkpoints: new Map(),
  renderValues: null,
  renderDescriptor: null,
  speed: 1,
  playing: false,
  busy: false,
  renderScale: null
};

const canvas = document.querySelector("#worldCanvas");
const context = canvas.getContext("2d", { alpha: false });
const offscreen = document.createElement("canvas");
const offContext = offscreen.getContext("2d", { alpha: false });

async function initialize() {
  const [states, fluxes, events] = await Promise.all([
    getJson("/api/states"),
    getJson("/api/fluxes"),
    getJson("/api/events/catalog")
  ]);
  state.catalog = states.sort((a, b) => a.order - b.order);
  state.catalogById = new Map(state.catalog.map(descriptor => [descriptor.id, descriptor]));
  state.fluxCatalog = fluxes.sort((a, b) => a.order - b.order);
  state.fluxById = new Map(state.fluxCatalog.map(descriptor => [descriptor.id, descriptor]));
  state.eventCatalog = events;
  state.eventByKind = new Map(events.map(descriptor => [descriptor.kind, descriptor]));
  if (!state.catalogById.has(state.layer)) state.layer = state.catalog[0]?.id;
  document.querySelector("#stateCount").textContent = `${state.catalog.length} СОСТОЯНИЙ`;
  buildLayerNavigation();
  await selectLayer(state.layer, false);
  await refreshAll();
}

function buildLayerNavigation(filter = "") {
  const navigation = document.querySelector("#layerNavigation");
  const query = filter.trim().toLocaleLowerCase("ru");
  navigation.replaceChildren();
  const visible = state.catalog.filter(descriptor => {
    if (!query) return true;
    return `${descriptor.title} ${descriptor.shortTitle} ${descriptor.description} ${descriptor.groupTitle}`
      .toLocaleLowerCase("ru")
      .includes(query);
  });

  for (const group of uniqueGroups(visible)) {
    const label = document.createElement("div");
    label.className = "layer-group-label";
    label.textContent = group;
    navigation.append(label);
    for (const descriptor of visible.filter(item => item.groupTitle === group)) {
      const button = document.createElement("button");
      button.className = `layer-button${descriptor.id === state.layer ? " active" : ""}`;
      button.dataset.layer = descriptor.id;
      button.title = descriptor.description;
      button.innerHTML = `<span class="layer-swatch" style="--swatch:${descriptor.swatch}"></span><span>${descriptor.shortTitle}</span>${descriptor.kind === "Diagnostic" ? '<small>диагн.</small>' : ""}`;
      button.addEventListener("click", () => selectLayer(descriptor.id));
      navigation.append(button);
    }
  }

  if (!visible.length) {
    const empty = document.createElement("div");
    empty.className = "navigation-empty";
    empty.textContent = "Состояния не найдены";
    navigation.append(empty);
  }
}

function uniqueGroups(descriptors) {
  return [...new Set(descriptors.map(descriptor => descriptor.groupTitle))];
}

async function selectLayer(layer, load = true) {
  state.layer = layer;
  document.querySelectorAll(".layer-button").forEach(button => button.classList.toggle("active", button.dataset.layer === layer));
  const descriptor = currentDescriptor();
  document.querySelector("#layerGroup").textContent = descriptor.groupTitle;
  document.querySelector("#layerTitle").textContent = descriptor.title;
  document.querySelector("#layerDescription").textContent = descriptor.description;
  if (load) await refreshLayer();
  if (state.selectedCell) {
    await Promise.all([refreshExplanation(), refreshCellHistory()]);
    renderCellDetails(state.selectedCell);
  }
  updateCheckpointControls();
}

async function refreshExplanation() {
  if (!state.selected) {
    state.explanation = null;
    return;
  }

  state.explanation = await getJson(
    `/api/cell/${state.selected.x}/${state.selected.y}/explain/${state.layer}`);
}

async function refreshCellHistory() {
  if (!state.selected) {
    state.cellHistory = null;
    return;
  }

  state.cellHistory = await getJson(
    `/api/cell/${state.selected.x}/${state.selected.y}/history/${state.layer}?resolution=${state.historyResolution}`);
}

async function refreshAll() {
  const [summary, snapshot, history, regimes] = await Promise.all([
    getJson("/api/summary"),
    getJson(`/api/layer/${state.layer}`),
    getJson(`/api/history/world?resolution=${state.historyResolution}`),
    getJson("/api/events?limit=12")
  ]);
  state.summary = summary;
  state.snapshot = snapshot;
  state.worldHistory = history;
  state.regimes = regimes;
  updateSummary();
  renderMap();
  if (state.selected) await inspectCell(state.selected.x, state.selected.y, false);
}

async function refreshLayer() {
  try {
    state.snapshot = await getJson(`/api/layer/${state.layer}`);
    renderMap();
  } catch (error) { showError(error); }
}

function updateSummary() {
  const summary = state.summary;
  if (!summary) return;
  const hour = String(Math.floor(summary.hourOfDay)).padStart(2, "0");
  const minute = String(Math.floor(summary.hourOfDay % 1 * 60)).padStart(2, "0");
  const seasons = { winter: "зима", spring: "весна", summer: "лето", autumn: "осень" };
  document.querySelector("#clockPrimary").textContent = `Год ${summary.year} · день ${String(summary.dayOfYear).padStart(3, "0")}`;
  document.querySelector("#clockSecondary").textContent = `${hour}:${minute} · ${seasons[summary.season] ?? summary.season}`;
  document.querySelector("#worldBadge").textContent = `${summary.width} × ${summary.height} / ${(summary.width * summary.cellSizeMeters / 1000).toFixed(1)} км`;
  document.querySelector("#meanTemperature").textContent = summary.meanSurfaceTemperatureC.toFixed(1);
  document.querySelector("#meanBiomass").textContent = `${summary.meanLiveBiomassGm2.toFixed(0)} г/м²`;
  document.querySelector("#dayLength").textContent = `${summary.dayLengthHours.toFixed(1)} ч`;
  document.querySelector("#meanRain").textContent = `${summary.meanPrecipitationMmPerHour.toFixed(3)} мм/ч`;
  document.querySelector("#sunElevation").textContent = `${summary.sunElevationDegrees.toFixed(1)}°`;
  const cells = summary.width * summary.height;
  document.querySelector("#waterStored").textContent = (summary.waterBudget.storedMmCells / cells).toFixed(1);
  document.querySelector("#waterIn").textContent = `${(summary.waterBudget.externalInputMmCells / cells).toFixed(1)} мм`;
  document.querySelector("#waterOut").textContent = `${(summary.waterBudget.externalOutputMmCells / cells).toFixed(1)} мм`;
  document.querySelector("#waterError").textContent = `${(summary.waterBudget.relativeError * 100).toExponential(1)}%`;
  document.querySelector("#yearMarker").style.left = `${(summary.dayOfYear - 1) / 365 * 100}%`;
  document.querySelector("#dayProgress").style.width = `${summary.hourOfDay / 24 * 100}%`;
  renderWorldHistory();
  renderRegimeEvents();
}

const worldHistoryMetrics = {
  meanSurfaceTemperatureC: { label: "средняя температура", unit: "°C", precision: 1, color: "#e4b861" },
  storedWaterMmPerCell: { label: "вся запасённая вода", unit: "мм", precision: 1, color: "#65c7bb" },
  meanLiveBiomassGm2: { label: "живая растительная масса", unit: "г/м²", precision: 0, color: "#b9db67" },
  meanSnowMm: { label: "снежный запас", unit: "мм", precision: 1, color: "#b8d8df" }
};

function renderWorldHistory() {
  const history = state.worldHistory;
  const metricId = document.querySelector("#worldHistoryMetric")?.value ?? "meanSurfaceTemperatureC";
  const metric = worldHistoryMetrics[metricId];
  if (!history || !metric) return;
  drawHistoryChart(
    document.querySelector("#worldHistoryChart"),
    history.points,
    point => point[metricId],
    metric.color);
  document.querySelector("#worldHistoryCaption").textContent = history.points.length > 1
    ? `${metric.label}: ${history.points.length} точек · шаг ${formatPeriod(history.sampleIntervalHours)} · окно до ${formatPeriod(history.retentionHours)}`
    : `первая точка записана · следующий отсчёт через ${formatPeriod(history.sampleIntervalHours)}`;
}

function renderCellHistory() {
  const history = state.cellHistory;
  const button = document.querySelector("#pinCellButton");
  const caption = document.querySelector("#cellHistoryCaption");
  if (!history) {
    button.classList.remove("active");
    button.textContent = "закрепить";
    drawHistoryChart(document.querySelector("#cellHistoryChart"), [], point => point.value, "#65c7bb");
    caption.textContent = "Закрепите точку, чтобы состояние записывалось во времени.";
    return;
  }

  button.classList.toggle("active", history.isPinned);
  button.textContent = history.isPinned ? "открепить" : "закрепить";
  drawHistoryChart(
    document.querySelector("#cellHistoryChart"),
    history.points,
    point => point.value,
    currentDescriptor().swatch ?? "#65c7bb");
  caption.textContent = history.isPinned
    ? `${currentDescriptor().shortTitle}: ${history.points.length} точек · ${history.unit} · шаг ${formatPeriod(history.sampleIntervalHours)}`
    : "Закрепите точку, чтобы состояние записывалось во времени.";
}

function renderRegimeEvents() {
  const container = document.querySelector("#regimeEvents");
  const count = document.querySelector("#activeEventCount");
  const regimes = state.regimes;
  container.replaceChildren();
  if (!regimes) {
    count.textContent = "наблюдение";
    container.innerHTML = '<div class="events-empty">Детектору нужны первые сутки, чтобы отличить переход от суточного колебания.</div>';
    return;
  }

  count.textContent = regimes.active.length
    ? `${regimes.active.length} активно`
    : "спокойный режим";
  const episodes = [...regimes.active, ...regimes.recent].slice(0, 6);
  if (!episodes.length) {
    container.innerHTML = `<div class="events-empty">${state.summary?.elapsedHours < 24
      ? "Накапливаю первые сутки наблюдений."
      : "Подтверждённых режимных переходов пока нет."}</div>`;
    return;
  }

  for (const episode of episodes) {
    const descriptor = state.eventByKind.get(episode.kind);
    if (!descriptor) continue;
    const active = episode.endedAtHours === null;
    const button = document.createElement("button");
    button.type = "button";
    button.className = `regime-event${active ? " active" : ""}`;
    button.style.setProperty("--event-color", descriptor.color);
    button.title = `${descriptor.description} Открыть слой «${state.catalogById.get(descriptor.focusLayer)?.title ?? descriptor.focusLayer}».`;
    button.innerHTML = `
      <div class="event-heading"><strong>${descriptor.shortTitle}</strong><span>${active ? "идёт сейчас" : formatEventDuration(episode)}</span></div>
      <div class="event-reading"><span>${descriptor.indicatorTitle}</span><strong>${formatEventIndicator(episode.peakIndicator, descriptor)}</strong></div>
      <div class="event-bar"><i style="width:${Math.max(3, episode.peakSeverity * 100)}%"></i></div>
      <small class="event-evidence">пик ${formatWorldMoment(episode.peakAtHours)} · ${eventEvidence(episode)}</small>`;
    button.addEventListener("click", () => selectLayer(descriptor.focusLayer));
    container.append(button);
  }
}

function formatEventIndicator(value, descriptor) {
  if (descriptor.indicatorUnit === "доля") return `${formatNumber(value * 100, 0)}%`;
  const precision = descriptor.kind === "DustEpisode" ? 3 : 2;
  return `${formatNumber(value, precision)} ${descriptor.indicatorUnit}`;
}

function formatEventDuration(episode) {
  const duration = Math.max(0, episode.endedAtHours - episode.startedAtHours);
  return `${formatWorldMoment(episode.startedAtHours)} · ${formatCompactDuration(duration)}`;
}

function formatWorldMoment(elapsedHours) {
  const year = Math.floor(elapsedHours / (365 * 24)) + 1;
  const day = Math.floor(elapsedHours / 24) % 365 + 1;
  return `г${year} · д${day}`;
}

function formatCompactDuration(hours) {
  if (hours < 24) return `${formatNumber(hours, 0)} ч`;
  if (hours < 365 * 24) return `${formatNumber(hours / 24, hours % 24 ? 1 : 0)} дн`;
  return `${formatNumber(hours / (365 * 24), 1)} г`;
}

function eventEvidence(episode) {
  const metrics = episode.peakMetrics;
  switch (episode.kind) {
    case "Snowmelt":
      return `снег ${formatNumber(metrics.meanSnowMm, 1)} мм · покров ${formatNumber(metrics.snowCoveredFraction * 100, 0)}%`;
    case "FloodPulse":
      return `вода ${formatNumber(metrics.meanSurfaceWaterMm, 2)} мм · затоплено ${formatNumber(metrics.floodedFraction * 100, 1)}%`;
    case "GreenUp":
      return `биомасса ${formatNumber(metrics.meanLiveBiomassGm2, 0)} г/м² · зелёных ${formatNumber(metrics.greenFraction * 100, 0)}%`;
    case "Drought":
      return `корневая вода ${formatNumber(metrics.meanRootWaterMm, 1)} мм · стресс ${formatNumber(metrics.waterStressFraction * 100, 0)}%`;
    case "DustEpisode":
      return `ветер ${formatNumber(metrics.meanWindSpeedMs, 1)} м/с · затронуто ${formatNumber(metrics.dustAffectedFraction * 100, 1)}%`;
    default:
      return "числовой срез сохранён";
  }
}

async function togglePinnedCell() {
  if (!state.selected || state.busy) return;
  try {
    const method = state.cellHistory?.isPinned ? "DELETE" : "POST";
    await getJson(`/api/cell/${state.selected.x}/${state.selected.y}/pin`, { method });
    await refreshCellHistory();
    renderCellHistory();
  } catch (error) { showError(error); }
}

async function changeHistoryResolution(resolution) {
  state.historyResolution = resolution;
  try {
    const requests = [getJson(`/api/history/world?resolution=${resolution}`)];
    if (state.selected) {
      requests.push(getJson(`/api/cell/${state.selected.x}/${state.selected.y}/history/${state.layer}?resolution=${resolution}`));
    }
    const [worldHistory, cellHistory] = await Promise.all(requests);
    state.worldHistory = worldHistory;
    if (state.selected) state.cellHistory = cellHistory;
    renderWorldHistory();
    renderCellHistory();
  } catch (error) { showError(error); }
}

function drawHistoryChart(chart, points, readValue, color) {
  const rect = chart.getBoundingClientRect();
  const ratio = Math.min(2, window.devicePixelRatio || 1);
  const width = Math.max(1, Math.round(rect.width * ratio));
  const height = Math.max(1, Math.round(rect.height * ratio));
  if (chart.width !== width || chart.height !== height) {
    chart.width = width;
    chart.height = height;
  }
  const chartContext = chart.getContext("2d");
  chartContext.clearRect(0, 0, width, height);
  if (!points.length) return;

  const values = points.map(readValue).filter(Number.isFinite);
  if (!values.length) return;
  let minimum = Math.min(...values);
  let maximum = Math.max(...values);
  if (Math.abs(maximum - minimum) < 1e-8) {
    const padding = Math.max(1, Math.abs(maximum) * .05);
    minimum -= padding;
    maximum += padding;
  } else {
    const padding = (maximum - minimum) * .08;
    minimum -= padding;
    maximum += padding;
  }

  const left = 7 * ratio;
  const right = width - 7 * ratio;
  const top = 7 * ratio;
  const bottom = height - 7 * ratio;
  chartContext.strokeStyle = "rgba(151,168,153,.12)";
  chartContext.lineWidth = ratio * .65;
  chartContext.beginPath();
  chartContext.moveTo(left, (top + bottom) / 2);
  chartContext.lineTo(right, (top + bottom) / 2);
  if (minimum < 0 && maximum > 0) {
    const zeroY = bottom - (0 - minimum) / (maximum - minimum) * (bottom - top);
    chartContext.moveTo(left, zeroY);
    chartContext.lineTo(right, zeroY);
  }
  chartContext.stroke();

  chartContext.strokeStyle = color;
  chartContext.fillStyle = color;
  chartContext.lineWidth = ratio * 1.2;
  chartContext.beginPath();
  points.forEach((point, index) => {
    const x = points.length === 1 ? (left + right) / 2 : left + index / (points.length - 1) * (right - left);
    const y = bottom - (readValue(point) - minimum) / (maximum - minimum) * (bottom - top);
    if (index === 0) chartContext.moveTo(x, y); else chartContext.lineTo(x, y);
  });
  chartContext.stroke();
  const last = points.at(-1);
  const lastY = bottom - (readValue(last) - minimum) / (maximum - minimum) * (bottom - top);
  chartContext.beginPath();
  chartContext.arc(right, lastY, 2.1 * ratio, 0, Math.PI * 2);
  chartContext.fill();
}

function renderMap() {
  const snapshot = state.snapshot;
  if (!snapshot) return;
  const descriptor = currentDescriptor();
  let checkpoint = state.checkpoints.get(state.layer);
  if (checkpoint && (checkpoint.width !== snapshot.width || checkpoint.height !== snapshot.height)) {
    state.checkpoints.delete(state.layer);
    checkpoint = null;
  }
  const isDelta = state.mapMode === "delta" && checkpoint;
  let renderValues = snapshot.values;
  let renderDescriptor = descriptor;
  let statistics = snapshot.statistics;
  if (isDelta) {
    renderValues = snapshot.values.map((value, index) => stateDelta(value, checkpoint.values[index], descriptor));
    const finiteAbsolute = renderValues.filter(Number.isFinite).map(Math.abs).sort((a, b) => a - b);
    const observed = finiteAbsolute.length ? percentile(finiteAbsolute, .98) : 0;
    const floor = Math.pow(10, -Math.max(0, descriptor.precision));
    const maximumAbsolute = Math.max(floor, observed);
    renderDescriptor = {
      ...descriptor,
      scale: "Diverging",
      scaleMinimum: -maximumAbsolute,
      scaleMaximum: maximumAbsolute,
      palette: ["#a65d49", "#242d28", "#64c7bb"]
    };
    statistics = computeStatistics(renderValues, -maximumAbsolute, maximumAbsolute);
  }

  state.renderValues = renderValues;
  state.renderDescriptor = renderDescriptor;
  offscreen.width = snapshot.width;
  offscreen.height = snapshot.height;
  const image = offContext.createImageData(snapshot.width, snapshot.height);
  state.renderScale = { min: renderDescriptor.scaleMinimum, max: renderDescriptor.scaleMaximum };

  for (let index = 0; index < renderValues.length; index++) {
    const value = renderValues[index];
    const color = renderDescriptor.scale === "Categorical"
      ? hashColor(value)
      : interpolatePalette(renderDescriptor.palette, normalizeValue(value, renderDescriptor));
    const pixel = index * 4;
    image.data[pixel] = color[0];
    image.data[pixel + 1] = color[1];
    image.data[pixel + 2] = color[2];
    image.data[pixel + 3] = 255;
  }
  offContext.putImageData(image, 0, 0);
  sizeCanvas();
  updateLegend(renderDescriptor, { ...snapshot, statistics }, Boolean(isDelta), checkpoint);
  updateCheckpointControls();

  const vectorControl = document.querySelector("#vectorToggle").closest("label");
  vectorControl.classList.toggle("option-disabled", !snapshot.vectorX);
}

function updateLegend(descriptor, snapshot, isDelta = false, checkpoint = null) {
  const categorical = descriptor.scale === "Categorical";
  const minimum = categorical ? snapshot.minimum : descriptor.scaleMinimum;
  const maximum = categorical ? snapshot.maximum : descriptor.scaleMaximum;
  document.querySelector("#legendGradient").style.background = `linear-gradient(90deg, ${descriptor.palette.join(",")})`;
  document.querySelector("#legendMin").textContent = formatNumber(minimum, descriptor.precision);
  document.querySelector("#legendMax").textContent = formatNumber(maximum, descriptor.precision);
  document.querySelector("#legendUnit").textContent = descriptor.unit;

  const statistics = snapshot.statistics;
  document.querySelector("#distributionSummary").textContent = `p10 ${formatNumber(statistics.percentile10, descriptor.precision)} · медиана ${formatNumber(statistics.median, descriptor.precision)} · p90 ${formatNumber(statistics.percentile90, descriptor.precision)}`;
  document.querySelector("#scaleType").textContent = isDelta ? "изменение · ноль в центре" : ({
    Linear: "линейная шкала",
    Logarithmic: "усилены малые значения",
    Diverging: "ноль в центре",
    Cyclic: "циклическая шкала",
    Categorical: "категории"
  })[descriptor.scale] ?? descriptor.scale;

  const warning = document.querySelector("#scaleWarning");
  const outliers = statistics.belowScaleCount + statistics.aboveScaleCount + statistics.nonFiniteCount;
  warning.hidden = outliers === 0 || categorical;
  if (!warning.hidden) {
    const parts = [];
    if (statistics.belowScaleCount) parts.push(`${statistics.belowScaleCount} ниже`);
    if (statistics.aboveScaleCount) parts.push(`${statistics.aboveScaleCount} выше`);
    if (statistics.nonFiniteCount) parts.push(`${statistics.nonFiniteCount} нечисловых`);
    warning.textContent = `За пределами шкалы: ${parts.join(" · ")}. Цвет ограничен краем легенды.`;
  }

  const histogram = document.querySelector("#layerHistogram");
  const maximumBin = Math.max(1, ...statistics.histogram);
  histogram.innerHTML = statistics.histogram
    .map(count => `<i style="height:${Math.max(2, count / maximumBin * 100)}%" title="${count} ячеек"></i>`)
    .join("");

  document.querySelector("#layerDescription").textContent = isDelta
    ? `Изменение относительно контрольного снимка (${checkpoint.label}). Тёплый цвет — уменьшение, бирюзовый — увеличение. Ноль означает устойчивое место.`
    : currentDescriptor().description;
}

function stateDelta(current, baseline, descriptor) {
  if (!Number.isFinite(current) || !Number.isFinite(baseline)) return Number.NaN;
  if (descriptor.scale !== "Cyclic") return current - baseline;
  const span = descriptor.scaleMaximum - descriptor.scaleMinimum;
  return ((current - baseline + span / 2) % span + span) % span - span / 2;
}

function computeStatistics(values, minimum, maximum) {
  const finite = values.filter(Number.isFinite).sort((a, b) => a - b);
  const histogram = Array(32).fill(0);
  for (const value of finite) {
    const normalized = clamp((value - minimum) / Math.max(1e-9, maximum - minimum), 0, .999999);
    histogram[Math.floor(normalized * histogram.length)]++;
  }
  return {
    percentile10: finite.length ? percentile(finite, .1) : 0,
    median: finite.length ? percentile(finite, .5) : 0,
    percentile90: finite.length ? percentile(finite, .9) : 0,
    belowScaleCount: finite.filter(value => value < minimum).length,
    aboveScaleCount: finite.filter(value => value > maximum).length,
    nonFiniteCount: values.length - finite.length,
    histogram
  };
}

function percentile(sorted, position) {
  if (!sorted.length) return 0;
  const index = (sorted.length - 1) * position;
  const lower = Math.floor(index);
  const fraction = index - lower;
  return sorted[lower] + ((sorted[lower + 1] ?? sorted[lower]) - sorted[lower]) * fraction;
}

function sizeCanvas() {
  if (!state.snapshot) return;
  const rect = canvas.getBoundingClientRect();
  const ratio = Math.min(2, window.devicePixelRatio || 1);
  const width = Math.max(1, Math.floor(rect.width * ratio));
  const height = Math.max(1, Math.floor(rect.height * ratio));
  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width;
    canvas.height = height;
  }
  drawCanvas();
}

function drawCanvas() {
  if (!state.snapshot) return;
  context.imageSmoothingEnabled = false;
  context.drawImage(offscreen, 0, 0, canvas.width, canvas.height);
  const scale = state.renderDescriptor?.scale ?? currentDescriptor().scale;
  if (document.querySelector("#contourToggle").checked && !["Categorical", "Cyclic"].includes(scale)) drawContours();
  if (document.querySelector("#vectorToggle").checked && state.snapshot.vectorX) drawVectors();
  if (state.selected) drawSelection(state.selected.x, state.selected.y);
}

function drawContours() {
  const { width, height } = state.snapshot;
  const values = state.renderValues ?? state.snapshot.values;
  const descriptor = state.renderDescriptor ?? currentDescriptor();
  const stepX = canvas.width / width;
  const stepY = canvas.height / height;
  context.save();
  context.strokeStyle = "rgba(235,241,222,.115)";
  context.lineWidth = Math.max(0.5, window.devicePixelRatio || 1);
  context.beginPath();
  const bands = 14;
  for (let y = 1; y < height; y++) {
    for (let x = 1; x < width; x++) {
      const index = y * width + x;
      const band = Math.floor(normalizeValue(values[index], descriptor) * bands);
      const leftBand = Math.floor(normalizeValue(values[index - 1], descriptor) * bands);
      const topBand = Math.floor(normalizeValue(values[index - width], descriptor) * bands);
      if (band !== leftBand) { context.moveTo(x * stepX, y * stepY); context.lineTo(x * stepX, (y + 1) * stepY); }
      if (band !== topBand) { context.moveTo(x * stepX, y * stepY); context.lineTo((x + 1) * stepX, y * stepY); }
    }
  }
  context.stroke();
  context.restore();
}

function drawVectors() {
  const snapshot = state.snapshot;
  if (!snapshot.vectorX || !snapshot.vectorY) return;
  const cellW = canvas.width / snapshot.width;
  const cellH = canvas.height / snapshot.height;
  const interval = Math.max(3, Math.floor(snapshot.width / 18));
  context.save();
  context.strokeStyle = "rgba(244,246,224,.74)";
  context.fillStyle = "rgba(244,246,224,.74)";
  context.lineWidth = Math.max(1, window.devicePixelRatio || 1);
  for (let y = interval / 2; y < snapshot.height; y += interval) {
    for (let x = interval / 2; x < snapshot.width; x += interval) {
      const index = Math.floor(y) * snapshot.width + Math.floor(x);
      const vx = snapshot.vectorX[index];
      const vy = snapshot.vectorY[index];
      const magnitude = Math.hypot(vx, vy);
      if (magnitude < 1e-5) continue;
      const length = interval * Math.min(cellW, cellH) * .34;
      const dx = vx / magnitude * length;
      const dy = vy / magnitude * length;
      const cx = x * cellW;
      const cy = y * cellH;
      context.beginPath();
      context.moveTo(cx - dx, cy - dy);
      context.lineTo(cx + dx, cy + dy);
      context.stroke();
      context.beginPath();
      context.arc(cx + dx, cy + dy, Math.max(1.3, window.devicePixelRatio), 0, Math.PI * 2);
      context.fill();
    }
  }
  context.restore();
}

function drawSelection(x, y) {
  const snapshot = state.snapshot;
  const cellW = canvas.width / snapshot.width;
  const cellH = canvas.height / snapshot.height;
  const cx = (x + .5) * cellW;
  const cy = (y + .5) * cellH;
  context.save();
  context.strokeStyle = "#f6f0d5";
  context.lineWidth = Math.max(1, window.devicePixelRatio || 1);
  context.beginPath();
  context.arc(cx, cy, Math.max(5, Math.min(cellW, cellH) * 1.8), 0, Math.PI * 2);
  context.moveTo(cx - 10, cy); context.lineTo(cx + 10, cy);
  context.moveTo(cx, cy - 10); context.lineTo(cx, cy + 10);
  context.stroke();
  context.restore();
}

function captureCheckpoint() {
  if (!state.snapshot || !state.summary) return;
  const hour = String(Math.floor(state.summary.hourOfDay)).padStart(2, "0");
  const checkpoint = {
    width: state.snapshot.width,
    height: state.snapshot.height,
    elapsedHours: state.summary.elapsedHours,
    label: `год ${state.summary.year}, день ${state.summary.dayOfYear}, ${hour}:00`,
    values: [...state.snapshot.values]
  };
  state.checkpoints.set(state.layer, checkpoint);
  state.mapMode = "value";
  updateCheckpointControls();
  renderMap();
}

function toggleMapMode() {
  if (!state.checkpoints.has(state.layer)) return;
  state.mapMode = state.mapMode === "delta" ? "value" : "delta";
  renderMap();
}

function updateCheckpointControls() {
  const checkpoint = state.checkpoints.get(state.layer);
  if (!checkpoint && state.mapMode === "delta") state.mapMode = "value";
  const captureButton = document.querySelector("#checkpointButton");
  const modeButton = document.querySelector("#mapModeButton");
  captureButton.textContent = checkpoint ? "обновить снимок" : "зафиксировать";
  captureButton.title = checkpoint ? `Контрольный снимок: ${checkpoint.label}` : "Сохранить текущую карту этого состояния для сравнения";
  modeButton.disabled = !checkpoint;
  modeButton.classList.toggle("active", state.mapMode === "delta" && Boolean(checkpoint));
  modeButton.textContent = state.mapMode === "delta" ? "текущее поле" : "Δ к снимку";
  modeButton.title = checkpoint ? `Сравнить с: ${checkpoint.label}` : "Сначала зафиксируйте карту";
}

async function inspectCell(x, y, redraw = true) {
  try {
    const [cell, explanation, history] = await Promise.all([
      getJson(`/api/cell/${x}/${y}`),
      getJson(`/api/cell/${x}/${y}/explain/${state.layer}`),
      getJson(`/api/cell/${x}/${y}/history/${state.layer}?resolution=${state.historyResolution}`)
    ]);
    state.selected = { x, y };
    state.selectedCell = cell;
    state.explanation = explanation;
    state.cellHistory = history;
    document.querySelector("#selectedCellLabel").textContent = `ячейка ${x}:${y}`;
    document.querySelector("#cellCoordinate").textContent = `x ${x} / y ${y}`;
    document.querySelector("#emptyCell").hidden = true;
    document.querySelector("#cellDetails").hidden = false;
    renderCellDetails(cell);
    if (redraw) drawCanvas();
  } catch (error) { showError(error); }
}

function renderCellDetails(cell) {
  const descriptor = currentDescriptor();
  const valueByState = new Map(cell.states.map(item => [item.state, item.value]));
  const selectedValue = valueByState.get(state.layer);
  const position = normalizeValue(selectedValue, descriptor);
  const status = describePosition(selectedValue, descriptor, cell);

  document.querySelector("#cellBiome").textContent = translateBiome(cell.biomeDescription);
  document.querySelector("#selectedStateTitle").textContent = descriptor.title;
  document.querySelector("#selectedStateStatus").textContent = status;
  document.querySelector("#selectedStateValue").textContent = formatCellState(descriptor, selectedValue, cell);
  document.querySelector("#selectedStateMarker").style.left = `${clamp(position, 0, 1) * 100}%`;
  document.querySelector("#selectedStateContext").textContent = `${descriptor.description} Статус относится к фиксированной шкале модели, а не к экологической норме.`;
  renderProcessExplanation(descriptor);
  renderCellHistory();

  const drainage = document.querySelector("#drainageLine");
  drainage.textContent = cell.drainToX === null
    ? "Сток: выход через границу мира"
    : `Следующий переход стока: ${cell.drainToX}:${cell.drainToY}`;

  const groups = document.querySelector("#cellStateGroups");
  groups.replaceChildren();
  for (const groupTitle of uniqueGroups(state.catalog)) {
    const descriptors = state.catalog.filter(item => item.groupTitle === groupTitle);
    const details = document.createElement("details");
    details.open = descriptors.some(item => item.id === state.layer);
    const summary = document.createElement("summary");
    summary.innerHTML = `<span>${groupTitle}</span><small>${descriptors.length}</small>`;
    details.append(summary);
    const values = document.createElement("div");
    values.className = "cell-values";
    for (const item of descriptors) {
      const row = document.createElement("button");
      row.type = "button";
      row.className = `cell-value${item.id === state.layer ? " active" : ""}`;
      row.dataset.state = item.id;
      row.innerHTML = `<span>${item.shortTitle}</span><strong>${formatCellState(item, valueByState.get(item.id), cell)}</strong>`;
      row.addEventListener("click", () => selectLayer(item.id));
      values.append(row);
    }
    details.append(values);
    groups.append(details);
  }
}

function renderProcessExplanation(descriptor) {
  const explanation = state.explanation;
  const contributions = document.querySelector("#fluxContributions");
  contributions.replaceChildren();
  if (!explanation || explanation.state !== descriptor.id) {
    document.querySelector("#changePeriod").textContent = "—";
    document.querySelector("#selectedStateDelta").textContent = "—";
    return;
  }

  document.querySelector("#changePeriod").textContent = formatPeriod(explanation.periodHours);
  const deltaElement = document.querySelector("#selectedStateDelta");
  deltaElement.textContent = `${formatSigned(explanation.delta, descriptor.precision)} ${descriptor.unit}`;
  deltaElement.className = explanation.delta > 1e-8 ? "positive" : explanation.delta < -1e-8 ? "negative" : "neutral";

  const maximum = Math.max(1e-8, ...explanation.contributions.map(item => Math.abs(item.stateContribution)));
  if (!explanation.contributions.length) {
    const empty = document.createElement("div");
    empty.className = "flux-empty";
    empty.textContent = explanation.periodHours > 0
      ? "Для этого состояния за период не записано материальных потоков."
      : "Сделайте шаг времени, чтобы увидеть процессы этого места.";
    contributions.append(empty);
  } else {
    for (const contribution of explanation.contributions) {
      const flux = state.fluxById.get(contribution.flux);
      const effect = contribution.stateContribution;
      const row = document.createElement("div");
      row.className = `flux-row ${effect >= 0 ? "incoming" : "outgoing"}`;
      row.title = flux?.description ?? contribution.flux;
      row.innerHTML = `
        <div class="flux-row-heading"><span>${flux?.shortTitle ?? contribution.flux}</span><strong>${formatSigned(effect, descriptor.precision)} ${descriptor.unit}</strong></div>
        <div class="flux-bar"><i style="width:${Math.max(2, Math.abs(effect) / maximum * 100)}%"></i></div>
        <small>${formatSigned(contribution.amount, flux?.precision ?? 3)} ${contribution.fluxUnit} потока</small>`;
      contributions.append(row);
    }
  }

  const unexplained = document.querySelector("#unexplainedChange");
  const tolerance = Math.max(1e-6, Math.abs(explanation.delta) * 1e-4);
  unexplained.hidden = Math.abs(explanation.unexplainedDelta) <= tolerance;
  if (!unexplained.hidden) {
    unexplained.textContent = `Пока не разложено по именованным процессам: ${formatSigned(explanation.unexplainedDelta, descriptor.precision)} ${descriptor.unit}. Это явный остаток модели, а не скрытая причина.`;
  }
}

function formatPeriod(hours) {
  if (hours <= 0) return "внешнее изменение";
  if (hours < 24) return `${formatNumber(hours, hours % 1 ? 1 : 0)} ч`;
  const days = hours / 24;
  if (days < 60) return `${formatNumber(days, days % 1 ? 1 : 0)} дн`;
  return `${formatNumber(days / 365, 2)} лет`;
}

function formatSigned(value, precision) {
  if (!Number.isFinite(value)) return "—";
  const roundedZero = Math.abs(value) < Math.pow(10, -precision) * .5;
  if (roundedZero) return formatNumber(0, precision);
  return `${value > 0 ? "+" : "−"}${formatNumber(Math.abs(value), precision)}`;
}

function describePosition(value, descriptor, cell) {
  if (descriptor.id === "Drainage") return cell.drainToX === null ? "выход" : "связана";
  if (descriptor.scale === "Categorical") return `категория ${Math.round(value)}`;
  if (descriptor.scale === "Cyclic") return cardinalDirection(value);
  if (value < descriptor.scaleMinimum) return "ниже шкалы";
  if (value > descriptor.scaleMaximum) return "выше шкалы";
  if (descriptor.scale === "Logarithmic" && Math.abs(value) < 1e-8) return "отсутствует";
  const normalized = normalizeValue(value, descriptor);
  if (normalized < .2) return "очень низкое";
  if (normalized < .4) return "низкое";
  if (normalized < .6) return "среднее";
  if (normalized < .8) return "высокое";
  return "очень высокое";
}

function formatCellState(descriptor, value, cell) {
  if (!Number.isFinite(value)) return "—";
  if (descriptor.id === "Drainage") return cell.drainToX === null ? "граница" : `→ ${cell.drainToX}:${cell.drainToY}`;
  if (descriptor.id === "Aspect") return `${value.toFixed(descriptor.precision)} рад · ${cardinalDirection(value)}`;
  if (descriptor.scale === "Categorical") return `#${Math.round(value)}`;
  return `${formatNumber(value, descriptor.precision)} ${descriptor.unit}`;
}

function cardinalDirection(radians) {
  const directions = ["В", "СВ", "С", "СЗ", "З", "ЮЗ", "Ю", "ЮВ"];
  const normalized = ((radians % (Math.PI * 2)) + Math.PI * 2) % (Math.PI * 2);
  return directions[Math.round(normalized / (Math.PI / 4)) % directions.length];
}

async function advance(hours) {
  if (state.busy) return;
  state.busy = true;
  setBusy(true);
  try {
    state.summary = await getJson(`/api/advance?hours=${hours}`, { method: "POST" });
    [state.snapshot, state.worldHistory, state.regimes] = await Promise.all([
      getJson(`/api/layer/${state.layer}`),
      getJson(`/api/history/world?resolution=${state.historyResolution}`),
      getJson("/api/events?limit=12")
    ]);
    updateSummary();
    renderMap();
    if (state.selected) await inspectCell(state.selected.x, state.selected.y, false);
  } catch (error) {
    state.playing = false;
    updatePlayButton();
    showError(error);
  } finally {
    state.busy = false;
    setBusy(false);
  }
}

async function playLoop() {
  if (!state.playing) return;
  await advance(state.speed);
  if (state.playing) window.setTimeout(playLoop, 160);
}

function updatePlayButton() {
  document.querySelector("#playButton").classList.toggle("playing", state.playing);
}

function setBusy(busy) {
  document.querySelector("#loading").classList.toggle("visible", busy);
  document.querySelectorAll(".step-buttons button, .speed-control button").forEach(button => button.disabled = busy);
}

async function resetWorld() {
  if (state.busy) return;
  const seedText = window.prompt("Seed нового мира", String(state.summary?.seed ?? 12345));
  if (seedText === null) return;
  const seed = Number.parseInt(seedText, 10);
  if (!Number.isInteger(seed)) return showError(new Error("Seed должен быть целым числом."));
  state.playing = false;
  updatePlayButton();
  state.busy = true;
  setBusy(true);
  try {
    state.summary = await getJson(`/api/reset?seed=${seed}&size=${state.summary?.width ?? 96}`, { method: "POST" });
    [state.snapshot, state.worldHistory, state.regimes] = await Promise.all([
      getJson(`/api/layer/${state.layer}`),
      getJson(`/api/history/world?resolution=${state.historyResolution}`),
      getJson("/api/events?limit=12")
    ]);
    state.selected = null;
    state.selectedCell = null;
    state.explanation = null;
    state.cellHistory = null;
    state.checkpoints.clear();
    state.mapMode = "value";
    document.querySelector("#emptyCell").hidden = false;
    document.querySelector("#cellDetails").hidden = true;
    updateSummary();
    renderMap();
    renderCellHistory();
  } catch (error) { showError(error); }
  finally { state.busy = false; setBusy(false); }
}

async function getJson(url, options) {
  const response = await fetch(url, options);
  if (!response.ok) {
    let detail = `HTTP ${response.status}`;
    try { detail = (await response.json()).message ?? detail; } catch { /* no body */ }
    throw new Error(detail);
  }
  return response.json();
}

function currentDescriptor() {
  const descriptor = state.catalogById.get(state.layer);
  if (!descriptor) throw new Error(`Состояние ${state.layer} отсутствует в каталоге.`);
  return descriptor;
}

function normalizeValue(value, descriptor) {
  if (!Number.isFinite(value)) return 0;
  if (descriptor.scale === "Cyclic") {
    const span = descriptor.scaleMaximum - descriptor.scaleMinimum;
    return (((value - descriptor.scaleMinimum) % span) + span) % span / span;
  }
  if (descriptor.scale === "Diverging" && descriptor.scaleMinimum < 0 && descriptor.scaleMaximum > 0) {
    return value <= 0
      ? .5 * clamp((value - descriptor.scaleMinimum) / -descriptor.scaleMinimum, 0, 1)
      : .5 + .5 * clamp(value / descriptor.scaleMaximum, 0, 1);
  }
  const linear = clamp((value - descriptor.scaleMinimum) / Math.max(1e-7, descriptor.scaleMaximum - descriptor.scaleMinimum), 0, 1);
  return descriptor.scale === "Logarithmic" ? Math.log10(1 + 9 * linear) : linear;
}

function interpolatePalette(colors, value) {
  const scaled = clamp(value, 0, 1) * (colors.length - 1);
  const index = Math.min(colors.length - 2, Math.floor(scaled));
  const fraction = scaled - index;
  const a = hexToRgb(colors[index]);
  const b = hexToRgb(colors[index + 1]);
  return [
    Math.round(a[0] + (b[0] - a[0]) * fraction),
    Math.round(a[1] + (b[1] - a[1]) * fraction),
    Math.round(a[2] + (b[2] - a[2]) * fraction)
  ];
}

function hexToRgb(hex) {
  const value = Number.parseInt(hex.slice(1), 16);
  return [(value >> 16) & 255, (value >> 8) & 255, value & 255];
}

function hashColor(value) {
  const hue = (Math.floor(value) * 137.508 + 92) % 360;
  return hslToRgb(hue / 360, .28, .29 + (Math.floor(value) % 3) * .055);
}

function hslToRgb(h, s, l) {
  if (s === 0) return [l * 255, l * 255, l * 255];
  const hue2rgb = (p, q, t) => { if (t < 0) t += 1; if (t > 1) t -= 1; if (t < 1/6) return p + (q - p) * 6 * t; if (t < 1/2) return q; if (t < 2/3) return p + (q - p) * (2/3 - t) * 6; return p; };
  const q = l < .5 ? l * (1 + s) : l + s - l * s;
  const p = 2 * l - q;
  return [hue2rgb(p, q, h + 1/3), hue2rgb(p, q, h), hue2rgb(p, q, h - 1/3)].map(value => Math.round(value * 255));
}

function translateBiome(value) {
  return ({
    "seasonal wetland": "сезонная низина", "snow-covered steppe": "степь под снегом",
    "meadow steppe": "луговая степь", "crusted semi-desert": "корковая полупустыня",
    "dry steppe": "сухая степь", "grass steppe": "травяная степь", "sparse steppe": "разреженная степь"
  })[value] ?? value;
}

function formatNumber(value, precision = 2) {
  if (!Number.isFinite(value)) return "—";
  return value.toLocaleString("ru-RU", { minimumFractionDigits: precision, maximumFractionDigits: precision });
}

function clamp(value, minimum, maximum) { return Math.min(maximum, Math.max(minimum, value)); }

function showError(error) {
  const toast = document.querySelector("#toast");
  toast.textContent = error.message ?? String(error);
  toast.classList.add("visible");
  window.setTimeout(() => toast.classList.remove("visible"), 4500);
}

canvas.addEventListener("click", event => {
  if (!state.snapshot) return;
  const rect = canvas.getBoundingClientRect();
  const x = clamp(Math.floor((event.clientX - rect.left) / rect.width * state.snapshot.width), 0, state.snapshot.width - 1);
  const y = clamp(Math.floor((event.clientY - rect.top) / rect.height * state.snapshot.height), 0, state.snapshot.height - 1);
  inspectCell(x, y);
});
canvas.addEventListener("mousemove", event => {
  if (!state.snapshot) return;
  const rect = canvas.getBoundingClientRect();
  const x = clamp(Math.floor((event.clientX - rect.left) / rect.width * state.snapshot.width), 0, state.snapshot.width - 1);
  const y = clamp(Math.floor((event.clientY - rect.top) / rect.height * state.snapshot.height), 0, state.snapshot.height - 1);
  const descriptor = currentDescriptor();
  const index = y * state.snapshot.width + x;
  const isDelta = state.mapMode === "delta" && state.checkpoints.has(state.layer);
  const value = isDelta ? state.renderValues[index] : state.snapshot.values[index];
  document.querySelector("#cellCoordinate").textContent = `x ${x} / y ${y} · ${isDelta ? "Δ " : ""}${formatNumber(value, descriptor.precision)} ${descriptor.unit}`;
});
document.querySelector("#playButton").addEventListener("click", () => {
  state.playing = !state.playing;
  updatePlayButton();
  if (state.playing) playLoop();
});
document.querySelectorAll(".speed-control button").forEach(button => button.addEventListener("click", () => {
  state.speed = Number(button.dataset.speed);
  document.querySelectorAll(".speed-control button").forEach(item => item.classList.toggle("active", item === button));
}));
document.querySelectorAll(".step-buttons button").forEach(button => button.addEventListener("click", () => advance(Number(button.dataset.hours))));
document.querySelector("#contourToggle").addEventListener("change", drawCanvas);
document.querySelector("#vectorToggle").addEventListener("change", drawCanvas);
document.querySelector("#checkpointButton").addEventListener("click", captureCheckpoint);
document.querySelector("#mapModeButton").addEventListener("click", toggleMapMode);
document.querySelector("#pinCellButton").addEventListener("click", togglePinnedCell);
document.querySelector("#worldHistoryMetric").addEventListener("change", renderWorldHistory);
document.querySelector("#historyResolution").addEventListener("change", event => changeHistoryResolution(event.target.value));
document.querySelector("#resetButton").addEventListener("click", resetWorld);
document.querySelector("#layerSearch").addEventListener("input", event => buildLayerNavigation(event.target.value));
window.addEventListener("resize", () => {
  sizeCanvas();
  renderWorldHistory();
  renderCellHistory();
});

initialize().catch(showError);
