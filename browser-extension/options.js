async function render() {
  const [displays, data] = await Promise.all([
    chrome.system.display.getInfo(),
    chrome.storage.local.get({ enabled: true, displayZooms: {} })
  ]);

  document.getElementById('enabled').checked = data.enabled;
  const root = document.getElementById('displays');
  root.textContent = '';

  for (const display of displays.filter(d => d.isEnabled !== false)) {
    const row = document.createElement('div');
    row.className = 'display';

    const text = document.createElement('div');
    const name = document.createElement('strong');
    name.textContent = display.name || display.id;
    const details = document.createElement('span');
    details.className = 'muted';
    details.textContent = `${display.id} · ${Math.round(display.dpiX || 96)} DPI · ${display.bounds.width}×${display.bounds.height}`;
    text.append(name, document.createElement('br'), details);

    const input = document.createElement('input');
    input.type = 'number';
    input.min = '0.3';
    input.max = '5';
    input.step = '0.05';
    input.placeholder = 'Auto';
    input.dataset.displayId = display.id;
    if (data.displayZooms?.[display.id] != null) input.value = data.displayZooms[display.id];

    row.append(text, input);
    root.appendChild(row);
  }
}

document.getElementById('save').addEventListener('click', async () => {
  const displayZooms = {};
  for (const input of document.querySelectorAll('input[data-display-id]')) {
    if (input.value.trim() !== '') {
      const value = Number(input.value);
      if (Number.isFinite(value) && value >= 0.3 && value <= 5) displayZooms[input.dataset.displayId] = value;
    }
  }
  await chrome.storage.local.set({ enabled: document.getElementById('enabled').checked, displayZooms });
  document.getElementById('status').textContent = 'Saved';
  setTimeout(() => document.getElementById('status').textContent = '', 1200);
});

document.getElementById('reset').addEventListener('click', async () => {
  await chrome.storage.local.set({ enabled: true, displayZooms: {} });
  await render();
  document.getElementById('status').textContent = 'Automatic mode restored';
});

render();
