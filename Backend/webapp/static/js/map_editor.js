let mapRows = 37, mapCols = 44;
let currentTile = 'R';
let isDragging = false;
let mapLayout = Array(mapRows).fill().map(() => '.'.repeat(mapCols).split(''));

const renderGrid = () => {
    $('#grid').empty();
    for (let y = 0; y < mapRows; y++) {
    for (let x = 0; x < mapCols; x++) {
        const cell = $('<div></div>')
        .addClass('cell')
        .addClass(mapLayout[y][x] === 'g' ? 'ghost' : (mapLayout[y][x] === '.' ? 'dot' : mapLayout[y][x]))
        .attr('data-x', x)
        .attr('data-y', y)
        .text(mapLayout[y][x] === 'g' ? '' : mapLayout[y][x]);
        $('#grid').append(cell);
    }
    }
};

const applyTile = (x, y, tile) => {
    if (x < 1 || y < 1 || x >= mapCols || y >= mapRows) return;

    const current = mapLayout[y][x];
    console.log(current);

    if (current === "W" && tile !== "W") {
        // Clear W and ghost zone
        for (let dy = -1; dy <= 1; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            const ny = y + dy;
            const nx = x + dx;
            if (ny >= 0 && ny < mapRows && nx >= 0 && nx < mapCols) {
              if (mapLayout[ny][nx] === "W" || mapLayout[ny][nx] === "g") {
                mapLayout[ny][nx] = ".";
              }
            }
          }
        }
        renderGrid();
        return;
    }

    if (tile === "W") {
        if (x < 1 || y < 1 || x > mapCols - 2 || y > mapRows - 2) return;
    
        for (let dy = -1; dy <= 1; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            let val = mapLayout[y + dy][x + dx];
            if (val !== "." && val !== "R") return alert("Blocked area around center");
          }
        }
        mapLayout[y][x] = "W";
        for (let dy = -1; dy <= 1; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            if (dx === 0 && dy === 0) continue;
            mapLayout[y + dy][x + dx] = "g";
          }
        }
    }

    else if (current === "g" && tile === "R") {
        const directions = [
            [0, -1], // north
            [0, 1],  // south
            [-1, 0], // west
            [1, 0],  // east
        ];

        const isConnectedToWarehouse = directions.some(([dx, dy]) => {
            const nx = x + dx;
            const ny = y + dy;
            return nx >= 0 && ny >= 0 && nx < mapCols && ny < mapRows && mapLayout[ny][nx] === "W";
        });

        if (isConnectedToWarehouse) {
            mapLayout[y][x] = "R";
        } else {
            return alert("Road must be adjacent to warehouse (N/S/E/W only).");
        }
    }

    else {
        if (["B", "W", "M", "g"].includes(current)) {
            if (!confirm(`Replace ${current}?`)) return;
        }
        mapLayout[y][x] = tile;
    }
    renderGrid();
};

$(document).ready(function () {
    renderGrid();
    var timer = null;
    $(document).on('mousedown', '.cell', function (e) {
    e.preventDefault();
    //isDragging = false;
    //if (timer) clearInterval(timer);
    //timer = setInterval(function(){isDragging = true}, 100);
    //isDragging = true;
    const x = parseInt($(this).data('x'));
    const y = parseInt($(this).data('y'));
    applyTile(x, y, currentTile);
    });

    $(document).on('mouseenter', '.cell', function () {
    if (isDragging) {
        const x = parseInt($(this).data('x'));
        const y = parseInt($(this).data('y'));
        applyTile(x, y, currentTile);
    }
    });

    function mouseDragStop(ev) {
    //clearInterval(timer);
    isDragging = false;
    }
    //$(document).on("click", () => isDragging = true); 
    $(document).on('mouseup', mouseDragStop);
    $(document).on('mouseleave', mouseDragStop);
    $('#grid').on('mouseleave', mouseDragStop);
    
    $('.select-tile').click(function () {
    currentTile = $(this).data('tile');
    $('.select-tile').removeClass('active');
    $(this).addClass('active');
    });

    $('#export').click(function () {
    const result = mapLayout.map(row => row.join(''));
    $('#jsonOutput').val(JSON.stringify(result, null, 2));
    });

    $('#import').click(function () {
    try {
        const data = JSON.parse($('#jsonOutput').val());
        mapLayout = data.map(row => row.split(''));
        renderGrid();
    } catch (e) {
        alert('Invalid JSON');
    }
    });
});


function saveLayoutsToLocalStorage() {
    localStorage.setItem("mapLayouts", JSON.stringify(layouts));
  }
  
  function loadLayoutsFromLocalStorage() {
    let data = localStorage.getItem("mapLayouts");
    if (data) layouts = JSON.parse(data);
    updateLayoutSelector();
  }
  
  function updateLayoutSelector() {
    const selector = $("#layoutSelector");
    selector.empty();
    selector.append(`<option disabled selected>-- Select Layout --</option>`);
    for (let name in layouts) {
      selector.append(`<option value="${name}">${name}</option>`);
    }
  }
  
// Layouts map
let layouts = {};

// Save button
$("#saveLayout").click(function () {
let name = $("#layoutName").val().trim();
if (!name) return alert("Enter layout name");
layouts[name] = mapLayout.map(row => row.join(""));
saveLayoutsToLocalStorage();
updateLayoutSelector();
alert("Saved!");
});

// Delete
$("#deleteLayout").click(function () {
let name = $("#layoutSelector").val();
if (!name || !layouts[name]) return;
if (confirm("Delete this layout?")) {
    delete layouts[name];
    saveLayoutsToLocalStorage();
    updateLayoutSelector();
}
});

// Load selected
$("#layoutSelector").change(function () {
let name = $(this).val();
if (!name || !layouts[name]) return;
mapLayout = layouts[name].map(row => row.split(""));
renderGrid();
});

loadLayoutsFromLocalStorage();

$("#clearMap").click(function () {
    if (!confirm("Clear the entire map?")) return;
    mapLayout = Array(mapRows).fill().map(() => '.'.repeat(mapCols).split(''));
    renderGrid();
});
  
  