using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CampusLostAndFound.Models;

namespace CampusLostAndFound.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            try
            {
                // Ensure database created and tables exist
                if (context.Database.IsRelational())
                {
                    await context.Database.EnsureCreatedAsync();
                }

                // 1. Seed Roles
                string[] roleNames = {
                    "Student/Staff",
                    "SecurityPersonnel",
                    "CampusAdministrator",
                    "SystemAdministrator"
                };

                foreach (var roleName in roleNames)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        await roleManager.CreateAsync(new IdentityRole(roleName));
                        logger.LogInformation("Created role: {Role}", roleName);
                    }
                }

                // 2. Seed Users
                var usersToSeed = new[]
                {
                    new {
                        Email = "sysadmin@campus.edu",
                        FullName = "System Administrator",
                        RegNo = "SYS-001",
                        Role = "SystemAdministrator",
                        Password = "Admin@12345"
                    },
                    new {
                        Email = "campusadmin@campus.edu",
                        FullName = "Campus Administrator",
                        RegNo = "ADM-101",
                        Role = "CampusAdministrator",
                        Password = "Admin@12345"
                    },
                    new {
                        Email = "security@campus.edu",
                        FullName = "Officer Vikram Sharma",
                        RegNo = "SEC-042",
                        Role = "SecurityPersonnel",
                        Password = "Security@12345"
                    },
                    new {
                        Email = "john.doe@campus.edu",
                        FullName = "John Doe",
                        RegNo = "21BCE1042",
                        Role = "Student/Staff",
                        Password = "Student@12345"
                    },
                    new {
                        Email = "jane.smith@campus.edu",
                        FullName = "Jane Smith",
                        RegNo = "21BCE1180",
                        Role = "Student/Staff",
                        Password = "Student@12345"
                    }
                };

                var createdUsers = new Dictionary<string, ApplicationUser>();

                foreach (var u in usersToSeed)
                {
                    var existingUser = await userManager.FindByEmailAsync(u.Email);
                    if (existingUser == null)
                    {
                        var newUser = new ApplicationUser
                        {
                            UserName = u.Email,
                            Email = u.Email,
                            FullName = u.FullName,
                            RegistrationNumber = u.RegNo,
                            EmailConfirmed = true,
                            PhoneNumber = "+91 9876543210",
                            CreatedAt = DateTime.UtcNow
                        };

                        var result = await userManager.CreateAsync(newUser, u.Password);
                        if (result.Succeeded)
                        {
                            await userManager.AddToRoleAsync(newUser, u.Role);
                            createdUsers[u.Email] = newUser;
                            logger.LogInformation("Seeded user: {Email} ({Role})", u.Email, u.Role);
                        }
                    }
                    else
                    {
                        createdUsers[u.Email] = existingUser;
                    }
                }

                // 3. Seed Categories
                if (!await context.ItemCategories.AnyAsync())
                {
                    var categories = new List<ItemCategory>
                    {
                        new() { CategoryName = "Electronics", Description = "Laptops, phones, chargers, earphones, calculators, and gadgets", RetentionPeriodDays = 90 },
                        new() { CategoryName = "Documents", Description = "Mark sheets, certificates, transcripts, folders, and official files", RetentionPeriodDays = 120 },
                        new() { CategoryName = "Bags", Description = "Backpacks, handbags, laptop bags, and pouches", RetentionPeriodDays = 60 },
                        new() { CategoryName = "Books", Description = "Textbooks, reference manuals, novels, and notebooks", RetentionPeriodDays = 60 },
                        new() { CategoryName = "Clothing", Description = "Jackets, hoodies, sweaters, caps, and scarves", RetentionPeriodDays = 45 },
                        new() { CategoryName = "Accessories", Description = "Wallets, watches, eyeglasses, jewellery, and umbrellas", RetentionPeriodDays = 60 },
                        new() { CategoryName = "Keys", Description = "Vehicle keys, hostel room keys, and keychains", RetentionPeriodDays = 90 },
                        new() { CategoryName = "ID Cards", Description = "Campus student cards, national identity cards, driving licenses", RetentionPeriodDays = 180 },
                        new() { CategoryName = "Other", Description = "Miscellaneous or uncategorized items", RetentionPeriodDays = 60 }
                    };

                    await context.ItemCategories.AddRangeAsync(categories);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded item categories.");
                }

                // 4. Seed Campus Locations
                if (!await context.CampusLocations.AnyAsync())
                {
                    var locations = new List<CampusLocation>
                    {
                        new() { BuildingName = "Central Library", BlockName = "Main", Floor = "1st Floor", AreaName = "Reference & Reading Hall", Description = "Silent study area, near table 14" },
                        new() { BuildingName = "Academic Block A", BlockName = "A", Floor = "Ground Floor", AreaName = "Lecture Hall 102", Description = "Main lecture hall wing" },
                        new() { BuildingName = "Academic Block B", BlockName = "B", Floor = "2nd Floor", AreaName = "Computer Lab 4", Description = "Advanced systems laboratory" },
                        new() { BuildingName = "Student Cafeteria", BlockName = "Block C", Floor = "Ground Floor", AreaName = "Central Dining Area", Description = "Near billing counter" },
                        new() { BuildingName = "Sports Complex", BlockName = "Indoor", Floor = "Ground Floor", AreaName = "Badminton Court", Description = "Court 2 seating bleachers" },
                        new() { BuildingName = "Main Auditorium", BlockName = "Convention", Floor = "Ground Floor", AreaName = "Auditorium Main Hall", Description = "Row H, center aisle" },
                        new() { BuildingName = "Science & Tech Block", BlockName = "Block S", Floor = "3rd Floor", AreaName = "Electronics & IoT Lab", Description = "Workstation bench 6" },
                        new() { BuildingName = "North Campus Parking", BlockName = "Zone 2", Floor = "Ground Floor", AreaName = "Two-Wheeler Parking Lot", Description = "Near gate 3 security post" }
                    };

                    await context.CampusLocations.AddRangeAsync(locations);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded campus locations.");
                }

                // 5. Seed Initial Items and Private Details if empty
                if (!await context.Items.AnyAsync())
                {
                    var libraryLoc = await context.CampusLocations.FirstAsync(l => l.BuildingName == "Central Library");
                    var cafeteriaLoc = await context.CampusLocations.FirstAsync(l => l.BuildingName == "Student Cafeteria");
                    var labLoc = await context.CampusLocations.FirstAsync(l => l.AreaName.Contains("Computer Lab"));
                    var parkingLoc = await context.CampusLocations.FirstAsync(l => l.BuildingName.Contains("Parking"));

                    var electronicsCat = await context.ItemCategories.FirstAsync(c => c.CategoryName == "Electronics");
                    var accessoriesCat = await context.ItemCategories.FirstAsync(c => c.CategoryName == "Accessories");
                    var keysCat = await context.ItemCategories.FirstAsync(c => c.CategoryName == "Keys");

                    var student1 = createdUsers["john.doe@campus.edu"];
                    var student2 = createdUsers["jane.smith@campus.edu"];
                    var securityUser = createdUsers["security@campus.edu"];

                    // Item 1: Found Laptop Charger
                    var foundCharger = new Item
                    {
                        Title = "Dell 65W USB-C Laptop Charger",
                        Description = "Found black Dell fast charger plugged into power socket under table 12 in the main reading hall.",
                        ReportType = ReportTypes.Found,
                        CategoryId = electronicsCat.CategoryId,
                        LocationId = libraryLoc.LocationId,
                        ItemDateTime = DateTime.UtcNow.AddDays(-2),
                        Status = ItemStatuses.Active,
                        ReporterId = student1.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    };
                    await context.Items.AddAsync(foundCharger);
                    await context.SaveChangesAsync();

                    // Private details for charger
                    await context.PrivateItemDetails.AddAsync(new PrivateItemDetail
                    {
                        ItemId = foundCharger.ItemId,
                        DistinctiveMarks = "Yellow insulation tape wrapped 2 inches from the Type-C connector pin",
                        HiddenFeatures = "Small scratch shaped like an 'X' on the bottom of the power brick",
                        InternalContents = "Has a black Dell-branded Velcro cable tie attached",
                        AdditionalIdentifiers = "Serial number barcode ending in 4982"
                    });

                    // Item 2: Lost Laptop Charger (matches Item 1!)
                    var lostCharger = new Item
                    {
                        Title = "Black Dell USB-C Laptop Charger",
                        Description = "Misplaced my original Dell 65W Type-C charger while studying for midterm exams in the central library reading hall.",
                        ReportType = ReportTypes.Lost,
                        CategoryId = electronicsCat.CategoryId,
                        LocationId = libraryLoc.LocationId,
                        ItemDateTime = DateTime.UtcNow.AddDays(-2).AddHours(-1),
                        Status = ItemStatuses.Reported,
                        ReporterId = student2.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    };
                    await context.Items.AddAsync(lostCharger);
                    await context.SaveChangesAsync();

                    // Item 3: Found Fossil Wallet
                    var foundWallet = new Item
                    {
                        Title = "Brown Leather Fossil Wallet",
                        Description = "Found a bi-fold genuine leather wallet left on the round table near the coffee station.",
                        ReportType = ReportTypes.Found,
                        CategoryId = accessoriesCat.CategoryId,
                        LocationId = cafeteriaLoc.LocationId,
                        ItemDateTime = DateTime.UtcNow.AddDays(-4),
                        Status = ItemStatuses.Active,
                        ReporterId = student2.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-4)
                    };
                    await context.Items.AddAsync(foundWallet);
                    await context.SaveChangesAsync();

                    // Private details for wallet
                    await context.PrivateItemDetails.AddAsync(new PrivateItemDetail
                    {
                        ItemId = foundWallet.ItemId,
                        DistinctiveMarks = "Small scuff on lower edge; initials 'JD' faintly stamped inside",
                        HiddenFeatures = "Hidden emergency cash zipper flap inside the banknote divider",
                        InternalContents = "Metro smart card, two passport photos, and campus ID card ending in 1042",
                        AdditionalIdentifiers = "Contains silver emergency locker key tucked in side slit"
                    });

                    // Item 4: Found Casio Scientific Calculator
                    var foundCalc = new Item
                    {
                        Title = "Casio Classwiz fx-991EX Calculator",
                        Description = "Black and white scientific calculator left behind on workstation 14 after morning lab exam.",
                        ReportType = ReportTypes.Found,
                        CategoryId = electronicsCat.CategoryId,
                        LocationId = labLoc.LocationId,
                        ItemDateTime = DateTime.UtcNow.AddDays(-1),
                        Status = ItemStatuses.Active,
                        ReporterId = student1.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    };
                    await context.Items.AddAsync(foundCalc);
                    await context.SaveChangesAsync();

                    await context.PrivateItemDetails.AddAsync(new PrivateItemDetail
                    {
                        ItemId = foundCalc.ItemId,
                        DistinctiveMarks = "Handwritten pencil formula on the inside of the slide-on protective cover",
                        HiddenFeatures = "Small green dot sticker on the battery cover corner",
                        InternalContents = "Original solar cell unit and backup battery",
                        AdditionalIdentifiers = "Name sticker peeled off leaving sticky residue on back"
                    });

                    // Item 5: Found Bike Key with Keychain
                    var foundKeys = new Item
                    {
                        Title = "Motorcycle Key with Marvel Keychain",
                        Description = "Single bike key with a miniature metallic Captain America shield keychain found near the bicycle stands.",
                        ReportType = ReportTypes.Found,
                        CategoryId = keysCat.CategoryId,
                        LocationId = parkingLoc.LocationId,
                        ItemDateTime = DateTime.UtcNow.AddDays(-3),
                        Status = ItemStatuses.Active,
                        ReporterId = student2.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-3)
                    };
                    await context.Items.AddAsync(foundKeys);
                    await context.SaveChangesAsync();

                    await context.PrivateItemDetails.AddAsync(new PrivateItemDetail
                    {
                        ItemId = foundKeys.ItemId,
                        DistinctiveMarks = "Metallic shield keychain has a chip on the blue paint",
                        HiddenFeatures = "Small blue plastic hostel room tag with handwritten '304'",
                        AdditionalIdentifiers = "Key blade has small notch near the bow"
                    });

                    // Seed Custody Record for found charger by Security officer
                    await context.CustodyRecords.AddAsync(new CustodyRecord
                    {
                        ItemId = foundCharger.ItemId,
                        CustodyLocation = "Central Security Desk - Locker Box C-04",
                        ResponsibleSecurityUserId = securityUser.Id,
                        ReceivedAt = DateTime.UtcNow.AddDays(-1),
                        Notes = "Handed in by student; logged in security custody register."
                    });

                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded initial items, private details, and custody records.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }
    }
}
