// ESP32 8-channel Wi-Fi stopwatch enclosure set — V1.7 assembly-checked + PG7 reinforcement fix
// Target: Prusa i3 MK3S, 0.4 mm nozzle, PETG
// V1.7: retains V1.6 assembly audit fixes and adds:
// - rear PG7 reinforcement rail is now perforated by the same 8 gland holes,
//   so no 1.2 mm membrane remains behind the openings,
// - button anti-flex ribs shortened so they do not collide with the lid locating lip,
// - main tray rear mounting-hole row corrected from 62 mm to 63 mm spacing,
// - RESET opening increased to 12.5 mm for a nominal 12 mm panel button,
// - V1.5 +10 mm button-base height and V1.4 lid orientation fix retained.
// Units: mm

$fn = 64;
PART = "main_base";
LANE = 1;

// Optimized wall system: 4 extrusion lines at ~0.4 mm
wall = 1.6;
floor_t = 1.8;
button_lid_skin = 2.2;
main_lid_skin = 1.8;
pg_hole = 12.5;     // PG7/PG9 nominal M12 thread + FDM clearance
button_hole = 24.6; // nominal 24 mm + FDM clearance

module rounded_rect_2d(w,d,r){
    hull(){
        translate([r,r]) circle(r=r);
        translate([w-r,r]) circle(r=r);
        translate([r,d-r]) circle(r=r);
        translate([w-r,d-r]) circle(r=r);
    }
}
module rounded_plate(w,d,t,r){ linear_extrude(height=t) rounded_rect_2d(w,d,r); }
module capsule_slot(len=12,dia=3,h=5){
    hull(){
        translate([-len/2,0,0]) cylinder(d=dia,h=h,$fn=24);
        translate([ len/2,0,0]) cylinder(d=dia,h=h,$fn=24);
    }
}
module hollow_round_frame(w,d,r,frame,h){
    difference(){
        linear_extrude(height=h) rounded_rect_2d(w,d,r);
        translate([frame,frame,-0.1])
            linear_extrude(height=h+0.2)
                rounded_rect_2d(w-2*frame,d-2*frame,max(r-frame,0.8));
    }
}
module screw_post_xy(x,y,z0,h,od=7.0,hole=2.6){
    difference(){
        translate([x,y,z0]) cylinder(d=od,h=h,$fn=40);
        translate([x,y,z0-0.1]) cylinder(d=hole,h=h+0.2,$fn=28);
    }
}

// ---------- BUTTON POD ----------
// Footprint deliberately retained for stability with children using a 60 mm button.
bw=90; bd=100; bh=55; br=7;
// Two diagonal lid screws; locating lip carries lateral load.
button_screws=[[9,9],[bw-9,bd-9]];

module button_base(){
    difference(){
        rounded_plate(bw,bd,bh,br);
        translate([wall,wall,floor_t])
            linear_extrude(height=bh-floor_t+1)
                rounded_rect_2d(bw-2*wall,bd-2*wall,max(br-wall,1));

        // PG7 cable gland, rear wall
        translate([bw/2,bd+1,25]) rotate([90,0,0]) cylinder(d=pg_hole,h=wall+3,$fn=48);

        // Drain/service pinhole
        translate([bw/2,bd-8,-0.1]) cylinder(d=2,h=floor_t+0.2,$fn=20);

        // Ballast/cable-tie slots: fit a steel flat bar/plate instead of printing dead weight.
        for (x=[bw/2-18,bw/2+18]) for (y=[27,50])
            translate([x,y,-0.1]) rotate([0,0,90]) capsule_slot(12,3,floor_t+0.2);

        // Four shallow rubber-foot recesses
        for (x=[13,bw-13]) for (y=[13,bd-13])
            translate([x,y,-0.1]) cylinder(d=11,h=0.8,$fn=40);
    }

    // Only two slim screw towers are needed because lid has an internal locating lip.
    for (p=button_screws)
        screw_post_xy(p[0],p[1],floor_t,bh-floor_t-1.5,7.0,2.6);

    // Small anti-flex ribs tying towers to nearest walls (very low mass).
    // Keep rib tops below the 3 mm locating lip (lip bottom is z=bh-3).
    // 1.2 mm vertical assembly clearance is left here.
    translate([wall,8,floor_t]) cube([8,1.4,bh-floor_t-4.2]);
    translate([bw-wall-8,bd-9.4,floor_t]) cube([8,1.4,bh-floor_t-4.2]);
}

module button_lid_assembly(lane=1,blank=false){
    lip_h=3.0; lip_t=1.2; lip_clear=0.35;
    lip_w=bw-2*(wall+lip_clear);
    lip_d=bd-2*(wall+lip_clear);
    lip_r=max(br-wall-lip_clear,1);

    // ASSEMBLY ORIENTATION:
    // exterior surface is on +Z; locating lip and ribs point down into the base.
    difference(){
        union(){
            rounded_plate(bw,bd,button_lid_skin,br);

            translate([wall+lip_clear,wall+lip_clear,-lip_h])
                hollow_round_frame(lip_w,lip_d,lip_r,lip_t,lip_h);

            translate([6,31,-1.8]) cube([bw-12,1.6,1.8]);
            translate([6,84,-1.8]) cube([bw-12,1.6,1.8]);
        }

        // Arcade button stem; cut through complete lid thickness and internal features.
        translate([bw/2,59,-lip_h-0.2]) cylinder(d=button_hole,h=lip_h+button_lid_skin+0.4,$fn=64);

        // Two M3 clearance holes in positions matching the base towers.
        for (p=button_screws)
            translate([p[0],p[1],-lip_h-0.2]) cylinder(d=3.4,h=lip_h+button_lid_skin+0.4,$fn=28);

        // Debossed lane number on the true exterior/top face.
        if (!blank)
            translate([bw/2,18,button_lid_skin-0.55])
                linear_extrude(height=0.65)
                    text(str(lane),size=19,halign="center",valign="center",font="DejaVu Sans:style=Bold");
    }
}

module button_lid(lane=1,blank=false){
    // PRINT ORIENTATION: rotate the COMPLETE assembled lid.
    // The model therefore looks mirrored from above in the slicer because the exterior is bed-facing.
    // After printing and flipping onto the base, screw positions and text are correct.
    translate([0,bd,button_lid_skin]) rotate([180,0,0]) button_lid_assembly(lane,blank);
}

// ---------- MAIN UNIT ----------
mw=160; md=110; mh=50; mr=8;
main_posts=[[8,10],[mw-8,10],[8,84],[mw-8,84]];

module main_base(){
    difference(){
        rounded_plate(mw,md,mh,mr);
        translate([wall,wall,floor_t])
            linear_extrude(height=mh-floor_t+1)
                rounded_rect_2d(mw-2*wall,md-2*wall,max(mr-wall,1));

        // 8 PG7 glands; rear corridor remains fully clear for locknuts.
        for (i=[0:7]) translate([14.5+i*18.7,md+1,17])
            rotate([90,0,0]) cylinder(d=pg_hole,h=wall+3,$fn=48);

        // Passive vents: elongated slots print cleaner than many small round holes.
        for (z=[19,31]) {
            translate([-0.1,md/2,z]) rotate([0,90,0]) capsule_slot(10,3.0,wall+0.3);
            translate([mw-wall-0.1,md/2,z]) rotate([0,90,0]) capsule_slot(10,3.0,wall+0.3);
        }
    }

    // Slimmer lid towers; lid locating lip handles alignment.
    for (p=main_posts)
        screw_post_xy(p[0],p[1],floor_t,mh-floor_t-1.5,7.2,2.6);

    // Universal tray supports, kept forward of PG7 service corridor.
    for (p=[[25,17],[mw-25,17],[25,80],[mw-25,80]])
        screw_post_xy(p[0],p[1],floor_t,6.0,7.2,2.6);

    // Local reinforcement rail only where PG7 nuts clamp the thin rear wall.
    // V1.7: perforate the rail with the same 8 PG7 holes. In V1.6 the rail was
    // added after the shell holes were cut, leaving a ~1.2 mm membrane behind them.
    difference(){
        translate([6,md-wall-1.2,8]) cube([mw-12,1.2,20]);
        for (i=[0:7]) translate([14.5+i*18.7,md+1,17])
            rotate([90,0,0]) cylinder(d=pg_hole,h=wall+5,$fn=48);
    }
}

module main_lid_assembly(blank=false){
    lip_h=3.0; lip_t=1.2; lip_clear=0.35;
    lip_w=mw-2*(wall+lip_clear);
    lip_d=md-2*(wall+lip_clear);
    lip_r=max(mr-wall-lip_clear,1);

    // ASSEMBLY ORIENTATION: exterior on +Z, lip/ribs downward into the base.
    difference(){
        union(){
            rounded_plate(mw,md,main_lid_skin,mr);

            translate([wall+lip_clear,wall+lip_clear,-lip_h])
                hollow_round_frame(lip_w,lip_d,lip_r,lip_t,lip_h);

            translate([39,7,-2.0]) cube([1.6,md-14,2.0]);
            translate([79,7,-2.0]) cube([1.6,md-14,2.0]);
            translate([119,7,-2.0]) cube([1.6,md-14,2.0]);
            translate([7,48,-2.0]) cube([mw-14,1.6,2.0]);
        }

        for (p=main_posts)
            translate([p[0],p[1],-lip_h-0.2]) cylinder(d=3.4,h=lip_h+main_lid_skin+0.4,$fn=28);

        if (!blank){
            translate([15,70,-lip_h-0.2]) cube([20.0,22.5,lip_h+main_lid_skin+0.4]);
            for (x=[72,94,116]) translate([x,82,-lip_h-0.2]) cylinder(d=5.3,h=lip_h+main_lid_skin+0.4,$fn=32);
            translate([140,82,-lip_h-0.2]) cylinder(d=12.5,h=lip_h+main_lid_skin+0.4,$fn=48);

            // Debossed labels on the true exterior/top face.
            translate([25,62,main_lid_skin-0.50]) linear_extrude(height=0.60)
                text("POWER",size=5,halign="center",valign="center",font="DejaVu Sans:style=Bold");
            translate([94,67,main_lid_skin-0.50]) linear_extrude(height=0.60)
                text("WIFI  DATA  ERR",size=4.6,halign="center",valign="center",font="DejaVu Sans:style=Bold");
            translate([140,62,main_lid_skin-0.50]) linear_extrude(height=0.60)
                text("RESET",size=4.8,halign="center",valign="center",font="DejaVu Sans:style=Bold");
            translate([mw/2,28,main_lid_skin-0.50]) linear_extrude(height=0.60)
                text("8-CHANNEL WIFI TIMER",size=7.2,halign="center",valign="center",font="DejaVu Sans:style=Bold");
        }
    }
}

module main_lid(blank=false){
    // PRINT ORIENTATION: rotate complete assembled lid, preserving physical alignment after flip.
    translate([0,md,main_lid_skin]) rotate([180,0,0]) main_lid_assembly(blank);
}

module main_tray(){
    tw=140; td=76; tt=1.8;
    difference(){
        rounded_plate(tw,td,tt,4);

        // Support holes, tray centered at x=10; y~10 in base.
        // Base supports are at global Y=17 and 80. With tray positioned at Y=10,
        // the local rows must therefore be Y=7 and 70 (63 mm spacing).
        for (x=[15,tw-15]) for (y=[7,70])
            translate([x,y,-0.1]) cylinder(d=3.4,h=tt+0.2,$fn=28);

        // Four large lightening windows. Remaining ribs form a stiff frame for cable ties/standoffs.
        for (x0=[12,76]) for (y0=[12,42])
            translate([x0,y0,-0.1])
                linear_extrude(height=tt+0.2)
                    rounded_rect_2d(52,22,3);

        // Universal tie slots around the windows.
        for (x=[20,48,92,120]) for (y=[8,38,68])
            translate([x,y,-0.1]) capsule_slot(10,2.8,tt+0.2);
    }
}

// First-print gauge. Button opening + PG7/RESET opening + clearance reference.
module fit_test(){
    difference(){
        cube([96,35,3]);
        translate([17,17.5,-0.1]) cylinder(d=button_hole,h=3.2,$fn=64);
        translate([49,17.5,-0.1]) cylinder(d=pg_hole,h=3.2,$fn=48);
        translate([76,17.5,-0.1]) cylinder(d=12.5,h=3.2,$fn=48);
    }
}

if (PART=="button_base") button_base();
else if (PART=="button_lid") button_lid(LANE,false);
else if (PART=="button_lid_blank") button_lid(1,true);
else if (PART=="button_lid_assembly") button_lid_assembly(LANE,false);
else if (PART=="main_base") main_base();
else if (PART=="main_lid") main_lid(false);
else if (PART=="main_lid_blank") main_lid(true);
else if (PART=="main_lid_assembly") main_lid_assembly(false);
else if (PART=="main_tray") main_tray();
else if (PART=="fit_test") fit_test();
else echo("Unknown PART");
