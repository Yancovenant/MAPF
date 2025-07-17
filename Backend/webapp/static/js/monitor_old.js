// monitor.js: Live video for YOLO Monitor

(function() {
    /**
  // Map agentId -> canvas context
  const canvases = {};
  document.querySelectorAll('canvas[id^="canvas-"]').forEach(canvas => {
    const agentId = canvas.id.replace('canvas-', '');
    canvases[agentId] = canvas.getContext('2d');
  });
  
  // Connect to backend WebSocket
  const ws = new WebSocket('ws://' + window.location.host + '/ws/monitor');
  ws.binaryType = 'arraybuffer';
  
  ws.onmessage = function(event) {
    const arr = new Uint8Array(event.data);
    let newline = arr.indexOf(10); // '\n'
    if (newline === -1) return;
    const header = JSON.parse(new TextDecoder().decode(arr.slice(0, newline)));
    const agentId = header.agent_id;
    const imgData = arr.slice(newline + 1);
    const ctx = canvases[agentId];
    if (!ctx) return;
    const img = new Image();
    img.onload = function() {
      ctx.clearRect(0, 0, 160, 120);
      ctx.drawImage(img, 0, 0, 160, 120);
      URL.revokeObjectURL(img.src);
    };
    img.src = URL.createObjectURL(new Blob([imgData], {type: 'image/jpeg'}));
    //console.log("Received frame for agent", agentId, "size", imgData.length);
  };
  
  ws.onopen = function() {
    console.log('[Monitor] Connected to backend');
  };
  ws.onclose = function() {
    console.log('[Monitor] Disconnected from backend');
  };
   */
    const canvases = {};
    const socket = new WebSocket("ws://" + location.host + "/ws/monitor");
    socket.binaryType = "arraybuffer";
  
    socket.onmessage = (event) => {
        const data = new Uint8Array(event.data);
        const newlineIndex = data.indexOf(10); // '\n'
  
        if (newlineIndex === -1) return;
  
        const header = JSON.parse(new TextDecoder().decode(data.slice(0, newlineIndex)));
        const imageData = data.slice(newlineIndex + 1);
        const agentId = header.agent_id;
        const detections = header.detections || [];
        const roadOutline = header.road_outline || [];
  
        const canvas = document.getElementById("canvas_" + agentId);
        if (!canvas) return;
  
        const ctx = canvas.getContext("2d");
  
        const blob = new Blob([imageData], { type: "image/jpeg" });
        const img = new Image();
        img.onload = () => {
            
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
            
            //canvas.width = img.width;
            //canvas.height = img.height;
            //ctx.drawImage(img, 0, 0);
  
            // Draw detections
            detections.forEach(det => {
                const [x, y, w, h] = det.bbox;
                const left = (x - w / 2) * (canvas.width / img.width);
                const top  = (y - h / 2) * (canvas.height / img.height);
                const boxW = w * (canvas.width / img.width);
                const boxH = h * (canvas.height / img.height);
                //const left = x - w / 2;
                //const top = y - h / 2;
  
                ctx.strokeStyle = det.on_road ? "red" : "lime";
                ctx.lineWidth = 1;
                //ctx.strokeRect(left, top, w, h);
                ctx.strokeRect(left, top, boxW, boxH);
  
                ctx.font = "10px sans-serif";
                ctx.fillStyle = ctx.strokeStyle;
                ctx.fillText(`${det.label} ${det.confidence}`, left, top - 2);
            });
  
            // ✅ Draw road outline if available
            if (roadOutline.length > 0) {
              //console.log("roadOutline", roadOutline);
              ctx.beginPath();
              ctx.strokeStyle = "yellow";
              ctx.lineWidth = 1;
              roadOutline.forEach((pt, i) => {
                //console.log(pt);
                const x = pt[0] * (canvas.width / img.width);
                const y = pt[1] * (canvas.height / img.height);
                if (i === 0) ctx.moveTo(x, y);
                else ctx.lineTo(x, y);
              });
              ctx.closePath();
              ctx.stroke();
            }
        };
        img.src = URL.createObjectURL(blob);
    };
  })(); 
  