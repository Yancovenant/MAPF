const TILE_TYPES = [
    {type: 'R', icon: 'fa-road', color: '#000', title: 'Road'},
    {type: 'B', icon: 'fa-building', color: '#BD2222', title: 'Building'},
    {type: 'W', icon: 'fa-warehouse', color: '#ff9800', title: 'Warehouse'},
    {type: 'S', icon: 'fa-flag', color: '#4caf50', title: 'Spawn'},
    {type: 'P', icon: 'fa-person', color: '#4caf50', title: 'Person'},
    {type: 'M', icon: 'fa-vector-square', color: '#FFF', title: 'Mat decoration'},
    {type: '.', icon: 'fa-square', color: '#888', title: 'Empty'}
]

const DEFAULT_ROWS = 37, DEFAULT_COLS = 44;
let currentTile = '.';
let mapData = [];

let isMouseDown = false;
let startCell = null;
let ghostRects = [];

let warehouseGhostRects = [];
let warehouseGhostValid = false;

let ghostLineValid = true;
let ghostLineFlashInterval = null;

$(function() {
    // on load
    _getLocalStorage();
    document.head.innerHTML += `<link rel="stylesheet" href="/static/css/map_editor.css">`;
    renderPalette();
    renderGrid();

    // btn events
    $('#saveMapBtn').on('click', saveMap);
    $('#loadMapBtn').on('click', loadMapList);
    $('#deleteMapBtn').on('click', deleteMap);
    $('#newMapBtn').on('click', newMap);
})

function renderPalette() {
    const $palette = $('#tilePalette');
    $palette.empty();
    TILE_TYPES.forEach(tile => {
        const $btn = $(`<button class="btn btn-dark tile-btn p-2 shadow-lg" data-type="${tile.type}" title="${tile.title}"><i class="fas fa-fw ${tile.icon}"></i></button>`);
        if (tile.type === currentTile) $btn.addClass('active');
        $btn.css('color', tile.color);
        $btn.on('click', function() {
            currentTile = tile.type;
            $('.tile-btn').removeClass('active');
            $btn.addClass('active');
        });
        $palette.append($btn);
    });
}

function getTileColor(type) {
    const tile = TILE_TYPES.find(t => t.type === type);
    return tile ? tile.color : '#23272b';
}

function renderGrid() {
    const width = $('#mapContainer').width();
    const height = $('#mapContainer').height();
    
    const stage = new Konva.Stage({
        container: '#mapContainer',
        width: width,
        height: height
    });
    const layer = new Konva.Layer();
    stage.add(layer);
    // console.log(mapData);
    for (let r = 0; r < mapData.length; r++) {
        for (let c = 0; c < mapData[r].length; c++) {
            const cell = mapData[r][c];
            if (cell === 'W') console.log(cell, getTileColor(cell));
            const rect = new Konva.Rect({
                x: c * 20,
                y: r * 20,
                width: 20,
                height: 20,
                fill: getTileColor(cell),
                stroke: '#333',
                strokeWidth: 1
            });
            rect.on('mousedown touchstart', (e) => {
                
                if (isInWarehouseArea(r, c) && currentTile !== '.' && currentTile !== 'R') {
                    return;
                }

                if (currentTile === 'R' && isWarehouseDiagonal(r, c)) {
                    return;
                }

                if (currentTile === 'W') {
                    if (warehouseGhostValid) {
                        placeWarehouse(r, c);
                    }
                    return;
                }
                if (cell === 'W') {
                    if (isWarehouseCenter(r, c)) {
                        removeWarehouse(r, c);
                    } else {
                        mapData[r][c] = currentTile;
                        rect.fill(getTileColor(currentTile));
                        layer.draw();
                        return;
                    }
                } else {
                    console.log(cell, "from renderGrid");
                }
                isMouseDown = true;
                startCell = {r, c};
                updateGhostLine(r, c);
            });
            rect.on('mouseover', (e) => {
                if (currentTile === 'W') {
                    updateWarehouseGhost(r, c);
                } else if (isMouseDown && startCell && currentTile !== 'W') {
                    updateGhostLine(r, c);
                }
            });
            rect.on('mouseup touchend', (e) => {
                if (currentTile === 'W') {
                    clearWarehouseGhost();
                }
                if (isMouseDown && startCell && currentTile !== 'W') {
                    applyGhostLine();
                }
                isMouseDown = false;
                startCell = null;
                clearGhostLine();
            })
            layer.add(rect);
        }
    }

    const ghostRect = new Konva.Rect({
        x: 0,
        y: 0,
        width: 20,
        height: 20,
        fill: '#00b89455',
        visible: false,
        listening: false
    });
    layer.add(ghostRect);

    stage.on('mousemove', (e) => {
        const pointer = stage.getPointerPosition();
        if (!pointer) return;
        const scale = stage.scaleX();
        const x = Math.floor((pointer.x - stage.x()) / (20 * scale));
        const y = Math.floor((pointer.y - stage.y()) / (20 * scale));
        
        if (x >= 0 && x < mapData[0]?.length && y >= 0 && y < mapData?.length) {
            ghostRect.position({x: x * 20, y: y * 20});
            ghostRect.visible(true);
            layer.batchDraw();
        } else {
            ghostRect.visible(false);
            layer.batchDraw();
        }
    });
    stage.on('mouseout', () => {
        ghostRect.visible(false);
        layer.batchDraw();
    });

    _autoScale(stage, layer);
    _onZoom(stage);
    layer.draw();

    function updateGhostLine(endR, endC) {
        clearGhostLine();
        if (!startCell) return;
        const {r: startR, c: startC} = startCell;
        
        const dx = endC - startC;
        const dy = endR - startR;

        ghostLineValid = true;
        let cells = [];


        if (Math.abs(dx) >= Math.abs(dy)) {
            // horizontal line
            const step = dx > 0 ? 1 : -1;
            for (let c = startC; c !== endC + step; c += step) {
                // ghostRects.push(drawGhostRect(startR, c));
                cells.push([startR, c]);
            }
        } else {
            // vertical line
            const step = dy > 0 ? 1 : -1;
            for (let r = startR; r !== endR + step; r += step) {
                // ghostRects.push(drawGhostRect(r, startC));
                cells.push([r, startC]);
            }
        }

        // validate
        for (const [r, c] of cells) {
            if (isInWarehouseArea(r, c) && currentTile !== '.' && currentTile !== 'R') {
                ghostLineValid = false;
            }
            if (currentTile === 'R' && isWarehouseDiagonal(r, c)) {
                ghostLineValid = false;
            }
        }

        for (const [r, c] of cells) {
            ghostRects.push(drawGhostRect(r, c, ghostLineValid));
        }

        layer.batchDraw();

        if (!ghostLineValid) {
            let flashOn = true;
            if (ghostLineFlashInterval) clearInterval(ghostLineFlashInterval);
            ghostLineFlashInterval = setInterval(() => {
                ghostRects.forEach(rect => {
                    rect.fill(flashOn ? '#e74c3cAA' : 'rgba(0,0,0,0)');
                });
                layer.batchDraw();
                flashOn = !flashOn;
            }, 150);
        } else if (ghostLineFlashInterval) {
            clearInterval(ghostLineFlashInterval);
            ghostLineFlashInterval = null;
        }
    }
    function clearGhostLine() {
        ghostRects.forEach(rect => rect.destroy());
        ghostRects = [];
        if (ghostLineFlashInterval) {
            clearInterval(ghostLineFlashInterval);
            ghostLineFlashInterval = null;
        }
        layer.batchDraw();
    }
    function drawGhostRect(r, c, isValid = true) {
        const ghost = new Konva.Rect({
            x: c * 20,
            y: r * 20,
            width: 20,
            height: 20,
            fill: isValid ? '#00b89455' : '#e74c3cAA',
            stroke: isValid ? '#00b894' : '#e74c3c',
            strokeWidth: 2,
            listening: false
        });
        layer.add(ghost);
        return ghost;
    }
    function applyGhostLine() {
        if (!startCell || ghostRects.length === 0) return;
        let valid = true;
        ghostRects.forEach(rect => {
            const r = Math.round(rect.y() / 20);
            const c = Math.round(rect.x() / 20);

            // prevent placing reserved warehouse cells except for '.' and 'R'
            if (isInWarehouseArea(r, c) && currentTile !== '.' && currentTile !== 'R') {
                valid = false;
                // optionatlly flash cell red
            }
            if (currentTile === 'R' && isWarehouseDiagonal(r, c)) {
                valid = false;
            }
        });
        if (valid) {
            ghostRects.forEach(rect => {
                const r = Math.round(rect.y() / 20);
                const c = Math.round(rect.x() / 20);
                mapData[r][c] = currentTile;
            });
            renderGrid();
        }
    }

    function updateWarehouseGhost(centerR, centerC) {
        clearWarehouseGhost();
        warehouseGhostValid = isWarehousePlacementValid(centerR, centerC);
        for (let dr = -1; dr <= 1; dr++) {
            for (let dc = -1; dc <= 1; dc++) {
                const r = centerR + dr;
                const c = centerC + dc;
                if (r < 0 || r >= mapData.length || c < 0 || c >= mapData[0].length) continue;
                let fill, stroke;
                if (dr === 0 && dc === 0) {
                    // center cell
                    fill = warehouseGhostValid ? '#FFD600AA' : '#e74c3cAA';
                    stroke = warehouseGhostValid ? '#FFD600' : '#e74c3c';
                } else {
                    // diagonal 
                    fill = warehouseGhostValid ? '#00b89455' : '#e74c3c55';
                    stroke = warehouseGhostValid ? '#00b894' : '#e74c3c';
                }
                const ghost = new Konva.Rect({
                    x: c * 20,
                    y: r * 20,
                    width: 20,
                    height: 20,
                    fill,
                    stroke,
                    strokeWidth: 2,
                    listening: false
                });
                warehouseGhostRects.push(ghost);
                layer.add(ghost);
            }
        }
        layer.batchDraw();
    }
    function clearWarehouseGhost() {
        warehouseGhostRects.forEach(rect => rect.destroy());
        warehouseGhostRects = [];
        layer.batchDraw();
    }
    function isWarehousePlacementValid(centerR, centerC) {
        // check 3x3 area
        for (let dr = -1; dr <= 1; dr++) {
            for (let dc = -1; dc <= 1; dc++) {
                const r = centerR + dr;
                const c = centerC + dc;
                if (r < 0 || r >= mapData.length || c < 0 || c >= mapData[0].length) return false;
                if (isInWarehouseArea(r, c) && !(dr === 0 && dc === 0 && mapData[r][c] === 'W')) return false;
                
                const val = mapData[r][c];
                
                // center: allow '.', 'R', or 'W' for overwrite/removal
                if (dr === 0 && dc === 0) {
                    if (val !== '.' && val !== 'R' && val !== 'W') return false;
                } else if (Math.abs(dr) === 1 && Math.abs(dc) === 1) {
                    // Diagonal must be '.'
                    if (val !== '.') return false;
                } else {
                    // Side: allow '.', 'R'
                    if (val !== '.' && val !== 'R') return false;
                }
            }
        }
        return true;
    }
    function placeWarehouse(centerR, centerC) {
        mapData[centerR][centerC] = 'W';
        renderGrid();
    }
    function isWarehouseCenter(r, c) {
        // check if this cell is the center of a 3x3 warehouse
        if (mapData[r][c] !== 'W') return false;
        if (r < 1 || r >= mapData.length - 1 || c < 1 || c >= mapData[0].length - 1) return false;
        for (let dr = -1; dr <= 1; dr++) {
            for (let dc = -1; dc <= 1; dc++) {
                const rr = r + dr, cc = c + dc;
                if (rr < 0 || rr >= mapData.length || cc < 0 || cc >= mapData[0].length) return false;
                if (dr === 0 && dc === 0) continue; // center
                if (Math.abs(dr) === 1 && Math.abs(dc) === 1) {
                    // diagonal
                    if (mapData[rr][cc] !== '.') return false;
                } else {
                    // side
                    if (mapData[rr][cc] !== '.' && mapData[rr][cc] !== 'R') return false;
                }
            }
        }
        return true;
    }
    function removeWarehouse(centerR, centerC) {
        if (mapData[centerR][centerC] === 'W') {
            mapData[centerR][centerC] = '.';
        }
        clearWarehouseGhost();
        renderGrid();
    }
    function isInWarehouseArea(r, c) {
        for (let rr = 1; rr < mapData.length - 1; rr++) {
            for (let cc = 1; cc < mapData[0].length - 1; cc++) {
                if (mapData[rr][cc] === 'W') {
                    if (Math.abs(rr - r) <= 1 && Math.abs(cc - c) <= 1) {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    function isWarehouseDiagonal(r, c) {
        for (let rr = 1; rr < mapData.length - 1; rr++) {
            for (let cc = 1; cc < mapData[0].length - 1; cc++) {
                if (mapData[rr][cc] === 'W') {
                    if (Math.abs(rr - r) === 1 && Math.abs(cc - c) === 1) {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    $(document).on('focusout blur beforeunload', _setLocalStorage);
    _setLocalStorage();
}



function _autoScale(stage) {
    const gridWidth = mapData[0]?.length || 1;
    const gridHeight = mapData?.length || 1;
    const tileSize = 20;
    
    const width = $('#mapContainer').width();
    const height = $('#mapContainer').height();
    const scale = Math.min(width / tileSize / gridWidth, height / tileSize / gridHeight);
    stage.scale({ x: scale, y: scale });

    const offSetX = (width - gridWidth * tileSize * scale) / 2;
    const offSetY = (height - gridHeight * tileSize * scale) / 2;
    const newPos = {
        x: offSetX,
        y: offSetY
    }

    stage.position(newPos);
}

function _onZoom(stage) {
    const scaleBy = 1.01;
    stage.on('wheel', (e) => {
        // stop default scrolling
        e.evt.preventDefault();

        const oldScale = stage.scaleX();
        const pointer = stage.getPointerPosition();

        const mousePointTo = {
            x: (pointer.x - stage.x()) / oldScale,
            y: (pointer.y - stage.y()) / oldScale,
        };

        // how to scale? Zoom in? Or zoom out?
        let direction = e.evt.deltaY > 0 ? 1 : -1;

        // when we zoom on trackpad, e.evt.ctrlKey is true
        // in that case lets revert direction
        if (e.evt.ctrlKey) {
            direction = -direction;
        }

        const newScale = direction > 0 ? oldScale * scaleBy : oldScale / scaleBy;

        stage.scale({ x: newScale, y: newScale });

        const newPos = {
            x: pointer.x - mousePointTo.x * newScale,
            y: pointer.y - mousePointTo.y * newScale,
        };
        stage.position(newPos);
    });
}

/**
 * Section of button click events
 */


function getMapName() {
    return $('#mapName').val().trim() || 'untitled';
}

function saveMap() {
    const name = getMapName();
    const layoutToSave = mapData.map(row => row.join(''));
    $.ajax({
        url: '/maps/save',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({name, layout: layoutToSave}),
        success: () => alert('Map saved!'),
        error: (xhr) => alert('Failed to save map. ' + xhr.responseJSON.error)
    });
}

function loadMapList() {
    $.get('/maps', function(maps) {
        const $list = $('#mapList');
        $list.empty().show();
        if (maps.length === 0) return;
        $list.append(`<option value="0" selected disabled>select map...</option>`);
        maps.forEach(name => $list.append(`<option value="${name}">${name}</option>`));
        $list.off('change').on('change', function() {
            loadMap($(this).val());
        });
    });
}

function loadMap(name) {
    $.get(`/maps/${name}`, function(data) {
        $('#mapName').val(name);
        mapData = data.layout.map(row => row.split(''));
        renderGrid();
    });
}

function deleteMap() {
    const name = getMapName();
    console.log(name);
    if (!confirm(`Delete map "${name}"?`)) return;
    if (!confirm(`This action cannot be undone, are you sure?`)) return;
    $.ajax({
        url: `/maps/delete`,
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({name}),
        success: () => { alert('Map deleted!'); location.reload(); },
        error: (xhr) => alert('Failed to delete map. ' + xhr.responseJSON.error)
    });
}

function newMap() {
    $('#mapName').val('untitled');
    // mapData = Array.from({length: DEFAULT_ROWS}, () => Array(DEFAULT_COLS).fill('empty'));
    mapData = [];
    for (let r = 0; r < DEFAULT_ROWS; r++) {
        mapData[r] = [];
        for (let c = 0; c < DEFAULT_COLS; c++) {
            mapData[r][c] = '.';
        }
    }
    renderGrid();
}

function _setLocalStorage() {
    const name = $("#mapName").val().trim() || 'untitled';
    const layout = mapData.map(row => row.join(''));
    localStorage.setItem(`mapEditorDraft`, JSON.stringify({name, layout}));
}

function _getLocalStorage() {
    const draft = localStorage.getItem(`mapEditorDraft`);
    if (!draft) return;
    try {
        const {name, layout} = JSON.parse(draft);
        $('#mapName').val(name);
        mapData = layout.map(row => row.split(''));
        renderGrid();
    } catch (e) {
        console.error('Failed to parse localStorage draft', e);
    }
}
