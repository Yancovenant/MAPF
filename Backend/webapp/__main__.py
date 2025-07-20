###
### webapp/__main__.py
###

import uvicorn
from .ASGI import application
from webapp.tools.config import recommend_settings

def main():
    uvicorn.run(application, host="0.0.0.0", port=8080)

if __name__ == "__main__":
    recommend_settings()
    main()