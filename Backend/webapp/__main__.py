###
### webapp/__main__.py
###

import uvicorn
from .ASGI import application
from webapp.tools.config import recommend_settings, configure_ports

server_port, unity_port = configure_ports()

def main():
    uvicorn.run(application, host="0.0.0.0", port=server_port)

if __name__ == "__main__":
    recommend_settings()
    print(f"[IMPORTANT]     Server running on port {server_port}")
    print(f"[IMPORTANT]     Unity running on port {unity_port}")
    print("===============================================")
    print("\n")
    main()