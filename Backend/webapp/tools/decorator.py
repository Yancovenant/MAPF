###
### webapp/tools/decorator.py
###

from starlette.routing import Route, WebSocketRoute
from starlette.responses import HTMLResponse

ROUTES = []

def endroute(path, methods=["GET"], con_type="http"):
    def decorator(func):
        if con_type == "ws" or "ws" in con_type:
            route_obj = WebSocketRoute(path, func)
        else:
            route_obj = Route(path, func, methods=methods)
        
        ROUTES.append(route_obj)
        return func
    return decorator

def render_layout(title, template):
    with open("webapp/static/xml/web_layout.xml", "r", encoding="utf-8") as f:
        web_layout = f.read()
    return HTMLResponse(
        web_layout.replace("<t t-title/>", title)
            .replace("<t t-out/>", template)
    )
