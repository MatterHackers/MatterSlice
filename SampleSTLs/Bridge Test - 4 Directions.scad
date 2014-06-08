difference()
{
	cube([30, 30, 10], center=true);
	translate([0,-15,-2]) rotate([0, 0, 45]) cube([15, 15, 10], center=true);
	translate([0,15,-2]) rotate([0, 0, 45]) cube([15, 15, 10], center=true);
	translate([15,0,-2]) rotate([0, 0, 45]) cube([15, 15, 10], center=true);
	translate([-15,0,-2]) rotate([0, 0, 45]) cube([15, 15, 10], center=true);
}
