// Monitor JavaScript for AUGV Dashboard
class AUGVMonitor {
    constructor() {
        this.ws = null;
        this.agents = new Map();
        this.init();
    }

    init() {
        this.connectWebSocket();
        this.setupEventListeners();
    }

    connectWebSocket() {
        const wsUrl = `ws://${window.location.host}/ws/monitor`;
        
        this.ws = new WebSocket(wsUrl);
        this.ws.binaryType = 'arraybuffer';

        this.ws.onopen = () => {
            console.log('Monitor WebSocket connected');
            this.updateStatus('Connected');
        };
        
        this.ws.onmessage = (event) => {
            this.handleMessage(event);
        };
        
        this.ws.onclose = () => {
            console.log('Monitor WebSocket disconnected');
            this.updateStatus('Disconnected');
            // Try to reconnect after 5 seconds
            setTimeout(() => this.connectWebSocket(), 5000);
        };
        
        this.ws.onerror = (error) => {
            console.error('WebSocket error:', error);
            this.updateStatus('Error');
        };
    }

    handleMessage(event) {
        try {
            // // Check if it's a ping message
            // if (event.data.startsWith('{"type":"ping"')) {
            //     return;
            // }
            // Parse the message
            const data = new Uint8Array(event.data);
            const lines = data.indexOf(10);
            if (lines === -1) return;

            const header = JSON.parse(new TextDecoder().decode(data.slice(0, lines)));

            const agentId = header.agent_id;
            const detections = header.detections || [];
            // Update agent status
            this.updateAgentStatus(agentId, detections);
            
            // Handle binary data (frame) if present
            const frameData = data.slice(lines + 1);
            if (frameData.length > 0) {
                this.displayFrame(agentId, frameData, detections);
            }
        } catch (error) {
            console.error('Error handling message:', error);
        }
    }

    updateAgentStatus(agentId, detections) {
        const agentElement = document.getElementById(`agent_${agentId}`);
        if (agentElement) {
            const statusElement = agentElement.querySelector('.status');
            if (statusElement) {
                const detectionCount = detections.length;
                statusElement.textContent = `Status: Active (${detectionCount} detections)`;
                statusElement.style.color = detectionCount > 0 ? '#e74c3c' : '#27ae60';
            }
        }
    }

    displayFrame(agentId, frameData, detections) {
        var canvas = document.getElementById(`canvas_${agentId}`);
        if (!canvas) {
            canvas = this.createAgentCanvas(agentId);
        }
        if (canvas) {
            const ctx = canvas.getContext('2d');
            const img = new Image();
            
            img.onload = () => {
                ctx.clearRect(0, 0, canvas.width, canvas.height);
                ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
                
                detections.forEach(det => {
                    // YOLO format: [center_x, center_y, width, height]
                    const [cx, cy, w, h] = det.bbox;
                    const left = (cx - w / 2) * (canvas.width / img.width);
                    const top  = (cy - h / 2) * (canvas.height / img.height);
                    const boxW = w * (canvas.width / img.width);
                    const boxH = h * (canvas.height / img.height);
                    ctx.beginPath();
                    ctx.rect(left, top, boxW, boxH);
                    ctx.strokeStyle = 'red';
                    ctx.lineWidth = 2;
                    ctx.stroke();
                    ctx.closePath();
                })
            };
            
            // Convert binary data to blob URL
            const blob = new Blob([frameData], { type: 'image/jpeg' });
            img.src = URL.createObjectURL(blob);
        }
    }

    updateStatus(status) {
        const statusElement = document.getElementById('connection-status');
        if (statusElement) {
            statusElement.textContent = `Connection: ${status}`;
            statusElement.className = `status ${status.toLowerCase()}`;
        }
    }

    createAgentCanvas(agentId) {
        const wrapper = document.getElementById('agents');
        if (!wrapper) return;
        const agentElement = document.createElement('div');
        agentElement.className = 'agent';
        agentElement.id = `agent_${agentId}`;
        wrapper.appendChild(agentElement);
        const n = document.createElement('div');
        n.className = 'agent-name';
        n.textContent = agentId;
        agentElement.appendChild(n);
        const s = document.createElement('div');
        s.className = 'status';
        s.textContent = 'Status: Disconnected';
        agentElement.appendChild(s);
        const c = document.createElement('canvas');
        c.id = `canvas_${agentId}`;
        agentElement.appendChild(c);
        return c;
    }

    setupEventListeners() {
        // Add any additional event listeners here
        window.addEventListener('beforeunload', () => {
            if (this.ws) {
                this.ws.close();
            }
        });
    }
}

// Initialize monitor when page loads
document.addEventListener('DOMContentLoaded', () => {
    new AUGVMonitor();
}); 