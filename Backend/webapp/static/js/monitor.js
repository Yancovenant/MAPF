// Modern CCTV Dashboard Monitor
class AUGVMonitor {
    constructor() {
        this.ws = null;
        this.agents = new Map();
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.init();
    }

    init() {
        this.connectWebSocket();
        this.setupEventListeners();
        this.updateConnectionStatus('Connecting...', 'connecting');
    }

    connectWebSocket() {
        const wsUrl = `ws://${window.location.host}/ws/monitor`;
        
        this.ws = new WebSocket(wsUrl);
        this.ws.binaryType = 'arraybuffer';

        this.ws.onopen = () => {
            console.log('Monitor WebSocket connected');
            this.updateConnectionStatus('Connected', 'connected');
            this.reconnectAttempts = 0;
        };
        
        this.ws.onmessage = (event) => {
            this.handleMessage(event);
        };
        
        this.ws.onclose = () => {
            console.log('Monitor WebSocket disconnected');
            this.updateConnectionStatus('Disconnected', 'disconnected');
            this.handleReconnection();
        };
        
        this.ws.onerror = (error) => {
            console.error('WebSocket error:', error);
            this.updateConnectionStatus('Connection Error', 'error');
        };
    }

    handleReconnection() {
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
            this.reconnectAttempts++;
            const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts), 10000);
            console.log(`Attempting to reconnect in ${delay}ms (attempt ${this.reconnectAttempts})`);
            
            setTimeout(() => {
                if (this.ws.readyState === WebSocket.CLOSED) {
                    this.connectWebSocket();
                }
            }, delay);
        } else {
            this.updateConnectionStatus('Connection Failed', 'error');
        }
    }

    handleMessage(event) {
        try {
            const data = new Uint8Array(event.data);
            const lineEnd = data.indexOf(10);
            if (lineEnd === -1) return;

            const header = JSON.parse(new TextDecoder().decode(data.slice(0, lineEnd)));
            const agentId = header.agent_id;
            const detections = header.detections || [];
            const status = header.status || 'active';
            
            // Update agent status
            this.updateAgentStatus(agentId, detections, status);
            
            // Handle binary data (frame) if present
            const frameData = data.slice(lineEnd + 1);
            if (frameData.length > 0) {
                this.displayFrame(agentId, frameData, detections);
            }
        } catch (error) {
            console.error('Error handling message:', error);
        }
    }

    updateAgentStatus(agentId, detections, status) {
        let agentElement = document.getElementById(`agent_${agentId}`);
        if (!agentElement) {
            agentElement = this.createAgentElement(agentId);
        }

        const statusIndicator = agentElement.querySelector('.status-indicator');
        const statusText = agentElement.querySelector('.status-text');
        const detectionCount = agentElement.querySelector('.detection-count');

        // Update status indicator
        statusIndicator.className = 'status-indicator';
        if (status === 'disconnected') {
            statusIndicator.classList.add('disconnected');
            statusText.textContent = 'Disconnected';
        } else if (detections.length > 0) {
            statusIndicator.classList.add('detection');
            statusText.textContent = `Detection (${detections.length})`;
        } else {
            statusIndicator.classList.add('active');
            statusText.textContent = 'Active';
        }

        // Update detection count
        if (detections.length > 0) {
            detectionCount.textContent = `${detections.length} detected`;
            detectionCount.style.display = 'block';
        } else {
            detectionCount.style.display = 'none';
        }

        // Remove empty state if agents exist
        this.removeEmptyState();
    }

    displayFrame(agentId, frameData, detections) {
        let canvas = document.getElementById(`canvas_${agentId}`);
        if (!canvas) {
            canvas = this.createAgentCanvas(agentId);
        }

        if (canvas) {
            $(".no-feed").hide();
            const ctx = canvas.getContext('2d');
            const img = new Image();
            
            img.onload = () => {
                // Set canvas size to match image aspect ratio
                const aspectRatio = img.width / img.height;
                canvas.width = canvas.offsetWidth;
                canvas.height = canvas.width / aspectRatio;
                
                ctx.clearRect(0, 0, canvas.width, canvas.height);
                ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
                
                // Draw detection boxes
                detections.forEach(det => {
                    if (det.bbox && det.bbox.length >= 4) {
                        const [cx, cy, w, h] = det.bbox;
                        const left = (cx - w / 2) * (canvas.width / img.width);
                        const top = (cy - h / 2) * (canvas.height / img.height);
                        const boxW = w * (canvas.width / img.width);
                        const boxH = h * (canvas.height / img.height);
                        
                        // Draw bounding box
                        ctx.beginPath();
                        ctx.rect(left, top, boxW, boxH);
                        ctx.strokeStyle = '#e74c3c';
                        ctx.lineWidth = 3;
                        ctx.stroke();
                        
                        // Draw feet point
                        const feetX = cx * (canvas.width / img.width);
                        const feetY = (cy + h/2) * (canvas.height / img.height);
                        ctx.beginPath();
                        ctx.arc(feetX, feetY, 6, 0, 2 * Math.PI);
                        ctx.fillStyle = '#3498db';
                        ctx.fill();
                        ctx.strokeStyle = '#ffffff';
                        ctx.lineWidth = 2;
                        ctx.stroke();
                    }
                });
            };
            
            // Convert binary data to blob URL
            const blob = new Blob([frameData], { type: 'image/jpeg' });
            img.src = URL.createObjectURL(blob);
        }
    }

    updateConnectionStatus(status, type) {
        const statusElement = document.getElementById('connection-status');
        if (statusElement) {
            statusElement.textContent = status;
            statusElement.className = 'badge';
            switch (type) {
                case 'connected':
                    statusElement.className = 'badge bg-success';
                    break;
                case 'disconnected':
                    statusElement.className = 'badge bg-danger';
                    break;
                case 'connecting':
                    statusElement.className = 'badge bg-warning';
            }
        }
    }

    createAgentElement(agentId) {
        const wrapper = document.getElementById('agents');
        if (!wrapper) return null;

        // Remove empty state
        this.removeEmptyState();

        const agentElement = document.createElement('div');
        agentElement.className = 'cctv-feed col position-relative d-flex flex-column';
        agentElement.id = `agent_${agentId}`;

        agentElement.innerHTML = `
            <div class="cctv-header d-flex justify-content-between align-items-center">
                <h3 class="agent-name h6 mb-0">
                    <i class="fas fa-robot"></i> ${agentId}
                </h3>
                <div class="agent-status d-flex align-items-center gap-2">
                    <span class="status-indicator disconnected"></span>
                    <span class="status-text small" style="color: #bdc3c7;">Disconnected</span>
                </div>
            </div>
            <div class="cctv-canvas-container position-relative">
                <div class="no-feed flex-column align-items-center justify-content-center h-100 fs-6">
                    <div class="loading-spinner"></div>
                    <div>Connecting...</div>
                </div>
                <canvas class="cctv-canvas" id="canvas_${agentId}"></canvas>
                <div class="detection-count" style="display: none;">0 detected</div>
                <div class="cctv-overlay">
                    <i class="fas fa-clock"></i> <span class="timestamp">--:--:--</span>
                </div>
            </div>
        `;

        wrapper.appendChild(agentElement);
        return agentElement;
    }

    createAgentCanvas(agentId) {
        const agentElement = document.getElementById(`agent_${agentId}`);
        if (!agentElement) return null;

        // Remove loading state
        const loadingDiv = agentElement.querySelector('.no-feed');
        console.log(loadingDiv);
        if (loadingDiv) {
            loadingDiv.style.display = 'none';
        }

        const canvas = agentElement.querySelector('.cctv-canvas');
        if (canvas) {
            canvas.style.display = 'block';
        }

        return canvas;
    }

    removeEmptyState() {
        const emptyState = document.querySelector('.empty-state');
        if (emptyState) {
            emptyState.remove();
        }
    }

    setupEventListeners() {
        window.addEventListener('beforeunload', () => {
            if (this.ws) {
                this.ws.close();
            }
        });

        // Handle window resize for responsive canvas
        window.addEventListener('resize', () => {
            this.resizeAllCanvases();
        });
    }

    resizeAllCanvases() {
        this.agents.forEach((agent, agentId) => {
            const canvas = document.getElementById(`canvas_${agentId}`);
            if (canvas && canvas.width !== canvas.offsetWidth) {
                // Trigger redraw if needed
                canvas.style.width = '100%';
            }
        });
    }
}

// Initialize monitor when page loads
document.addEventListener('DOMContentLoaded', () => {
    document.head.innerHTML += `<link rel="stylesheet" href="/static/css/monitor.css">`;
    new AUGVMonitor();
}); 