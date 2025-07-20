$(function(){
    var path = window.location.pathname;
    $('.sidebar .nav-link, .mobile-nav a').each(function(){
        if ($(this).attr('href') === path) {
            $(this).addClass('active');
        }
    });
});