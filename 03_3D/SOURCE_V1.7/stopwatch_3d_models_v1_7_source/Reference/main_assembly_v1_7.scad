include <../stopwatch_enclosures_v1_7.scad>
main_base();
translate([0,0,mh]) main_lid_assembly(false);
translate([10,10,floor_t+6]) main_tray();
