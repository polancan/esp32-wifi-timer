include <../stopwatch_enclosures_v1_7.scad>
if (TEST=="button") intersection(){ button_base(); translate([0,0,bh]) button_lid_assembly(1,false); }
else if (TEST=="main") intersection(){ main_base(); translate([0,0,mh]) main_lid_assembly(false); }
