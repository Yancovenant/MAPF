const WAYPOINTS = [
    "Warehouse_1", "Warehouse_2", "Warehouse_3", "Warehouse_4", "Warehouse_5",
    "Warehouse_6", "Warehouse_7", "Warehouse_8", "Warehouse_9", "Warehouse_10",
    "Warehouse_11", "Warehouse_12",
    "AUGV_1_Loadingspot", "AUGV_2_Loadingspot", "AUGV_3_Loadingspot", "AUGV_4_Loadingspot", "AUGV_5_Loadingspot"
];

const AGENTS = ["AUGV_1", "AUGV_2", "AUGV_3", "AUGV_4", "AUGV_5"];

function buildAgentFormSelector() {
    const $container = $('#agentFormContainer');
    $container.empty();
    AGENTS.forEach(agent => {
        const $col = $("<div class='col-md-6 mb-3'></div>");
        const $label = $(`<label for='${agent}'>${agent}</label>`);
        const $select = $(`<select class='form-select' name='${agent}' id='${agent}' multiple></select>`);
        WAYPOINTS.forEach(waypoint => {
            $select.append(`<option value='${waypoint}'>${waypoint}</option>`);
        });
        $col.append($label, $select);
        $container.append($col);
    })
}

$(function() {
    //buildAgentFormSelector();
    document.head.innerHTML += `<link rel="stylesheet" href="/static/css/client.css">`;
    
    $('#routeForm').on('submit', function(e){
        e.preventDefault();
        let data={action:'route',data:{}};
        $('#routeForm input').each(function(){
            let k=$(this).attr('name');
            try{data.data[k]=JSON.parse($(this).val());}catch{data.data[k]=[];}
        });
        $.ajax({
            url:'/send-routes',
            method:'POST',
            contentType:'application/json',
            data:JSON.stringify(data),
            success:function(res){$('#routeStatus').html('<div class=\'alert alert-success\'>Routes sent!</div>');},
            error:function(xhr){$('#routeStatus').html('<div class=\'alert alert-danger\'>Failed: '+xhr.responseText+'</div>');}
        });
    });
})