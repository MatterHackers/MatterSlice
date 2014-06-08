difference()
{
	cube([30, 30, 10], center=true);
	translate([0,-15,-2]) rotate([0, 0, 45]) cylinder(10, 7.5, 7.5, center=true);
	translate([0,15,-2]) rotate([0, 0, 45]) cylinder(10, 7.5, 7.5, center=true);
	translate([15,0,-2]) rotate([0, 0, 45]) cylinder(10, 7.5, 7.5, center=true);
	translate([-15,0,-2]) rotate([0, 0, 45]) cylinder(10, 7.5, 7.5, center=true);
}
