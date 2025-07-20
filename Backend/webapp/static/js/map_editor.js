const TILE_TYPES = [
    {type: 'R', icon: 'fa-road', color: '#000'},
    {type: 'B', icon: 'fa-building', color: '#795548'},
    {type: 'W', icon: 'fa-warehouse', color: '#ff9800'},
    {type: 'S', icon: 'fa-flag', color: '#4caf50'},
    {type: '.', icon: 'fa-square', color: '#888'}
]

const DEFAULT_ROWS = 37, DEFAULT_COLS = 44;
let currentTile = '.';
let mapData = [];

let isMouseDown = false;
let startCell = null;
let ghostRects = [];

$(function() {
    // on load
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
        const $btn = $(`<button class="btn btn-dark tile-btn p-2 shadow-lg" data-type="${tile.type}" title="${tile.type}"><i class="fas fa-fw ${tile.icon}"></i></button>`);
        if (tile.type === currentTile) $btn.addClass('active');
        $btn.css('color', tile.color);
        $btn.on('click', function() {
            currentTile = tile.type;
            $('.tile-btn').removeClass('active');
            $btn.addClass('active');
            console.log(currentTile, "from renderPalette");
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
                if (currentTile === 'W') {
                    mapData[r][c] = currentTile;
                    rect.fill(getTileColor(currentTile));
                    layer.draw();
                    return;
                }
                isMouseDown = true;
                startCell = {r, c};
                updateGhostLine(r, c);
            });
            rect.on('mouseover', (e) => {
                if (isMouseDown && startCell && currentTile !== 'W') {
                    updateGhostLine(r, c);
                }
            });
            rect.on('mouseup touchend', (e) => {
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

        if (Math.abs(dx) >= Math.abs(dy)) {
            // horizontal line
            const step = dx > 0 ? 1 : -1;
            for (let c = startC; c !== endC + step; c += step) {
                ghostRects.push(drawGhostRect(startR, c));
            }
        } else {
            // vertical line
            const step = dy > 0 ? 1 : -1;
            for (let r = startR; r !== endR + step; r += step) {
                ghostRects.push(drawGhostRect(r, startC));
            }
        }
        layer.batchDraw();
    }
    function clearGhostLine() {
        ghostRects.forEach(rect => rect.destroy());
        ghostRects = [];
        layer.batchDraw();
    }
    function drawGhostRect(r, c) {
        const ghost = new Konva.Rect({
            x: c * 20,
            y: r * 20,
            width: 20,
            height: 20,
            fill: '#00b89455',
            stroke: '#00b894',
            strokeWidth: 2,
            listening: false
        });
        layer.add(ghost);
        return ghost;
    }
    function applyGhostLine() {
        if (!startCell || ghostRects.length === 0) return;
        ghostRects.forEach(rect => {
            const r = Math.round(rect.y() / 20);
            const c = Math.round(rect.x() / 20);
            mapData[r][c] = currentTile;
        });
        renderGrid();
    }
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
