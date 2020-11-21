// using System;
// using System.Collections.Generic;
// using Microsoft.Extensions.DependencyInjection;
// using MongoDB.Bson;
// using UKSF.Api.Interfaces.Modpack;
// using UKSF.Api.Models.Integrations.Github;
// using UKSF.Api.Models.Modpack;
//
// namespace UKSF.Api.AppStart {
//     public static class TestDataSetup {
//         public static void Run(IServiceProvider serviceProvider) {
//             IReleaseService releaseService = serviceProvider.GetService<IReleaseService>();
//             releaseService.Data.Add(
//                 new ModpackRelease {
//                     id = ObjectId.GenerateNewId().ToString(),
//                     timestamp = DateTime.Now.AddDays(-15),
//                     version = "5.17.16",
//                     description =
//                         "Added captive escort animations and different radio backpacks, fixed backpack-on-chest errors, and arsenal adding extra mag error." +
//                         "\nUpdated ACRE, Lambs, and ZEN.",
//                     changelog = "#### Added" +
//                                 "\n- Captive escort animations, used base mod animations and custom implementation [(#683)](https://github.com/uksf/modpack/issues/683)" +
//                                 "\n<br>  _Note: there is no proper escort animation when unarmed_" +
//                                 "\n- Dynamic patrol area zone module [(#684)](https://github.com/uksf/modpack/issues/684)" +
//                                 "\n<br>  _See [Dynamic Patrols](https://github.com/uksf/modpack/wiki/Missions:-Dynamic-Patrols)_" +
//                                 "\n- Radio backpacks [(#687)](https://github.com/uksf/modpack/issues/687)" +
//                                 "\n\n#### Changed" +
//                                 "\n- Default radio channels for Apache and other aircraft" +
//                                 "\n- Resupply crates to have coded name abbreviations (e.g  (AM) = Ammo Mixed)" +
//                                 "\n- Use proper F-35 classname for rack init" +
//                                 "\n\n#### Fixed" +
//                                 "\n- Apache rotor hitbox, removed some hitpoints [(#685)](https://github.com/uksf/modpack/issues/685)" +
//                                 "\n- Arsenal adding extra mag when no 3CB weapon swap available [(#679)](https://github.com/uksf/modpack/issues/679)" +
//                                 "\n- Backpack-on-chest causing weapons and backpacks to be deleted [(#688)](https://github.com/uksf/modpack/issues/688)" +
//                                 "\n- Drone init not running for correct classname" +
//                                 "\n- Husky vanilla logistics values (removed them) [(#681)](https://github.com/uksf/modpack/issues/681)" +
//                                 "\n\n#### Updated" +
//                                 "\n- ACRE to [2.7.4.1027 + Dev](https://github.com/uksf/modpack/issues/691)" +
//                                 "\n- Lambs to [2.4.4](https://github.com/uksf/modpack/issues/690)" +
//                                 "\n- ZEN to [1.8.0](https://github.com/uksf/modpack/issues/689)" +
//                                 "\n\n[Report and track issues here](https://github.com/uksf/modpack/issues)\n"
//                 }
//             );
//             releaseService.Data.Add(
//                 new ModpackRelease {
//                     id = ObjectId.GenerateNewId().ToString(),
//                     timestamp = DateTime.Now.AddDays(-9),
//                     version = "5.17.17",
//                     description =
//                         "Added captive escort animations and different radio backpacks, fixed backpack-on-chest errors, and arsenal adding extra mag error." +
//                         "\nUpdated ACRE, Lambs, and ZEN.",
//                     changelog = "#### Added" +
//                                 "\n- Captive escort animations, used base mod animations and custom implementation [(#683)](https://github.com/uksf/modpack/issues/683)" +
//                                 "\n<br>  _Note: there is no proper escort animation when unarmed_" +
//                                 "\n- Dynamic patrol area zone module [(#684)](https://github.com/uksf/modpack/issues/684)" +
//                                 "\n<br>  _See [Dynamic Patrols](https://github.com/uksf/modpack/wiki/Missions:-Dynamic-Patrols)_" +
//                                 "\n- Radio backpacks [(#687)](https://github.com/uksf/modpack/issues/687)" +
//                                 "\n\n#### Changed" +
//                                 "\n- Default radio channels for Apache and other aircraft" +
//                                 "\n- Resupply crates to have coded name abbreviations (e.g  (AM) = Ammo Mixed)" +
//                                 "\n- Use proper F-35 classname for rack init" +
//                                 "\n\n#### Fixed" +
//                                 "\n- Apache rotor hitbox, removed some hitpoints [(#685)](https://github.com/uksf/modpack/issues/685)" +
//                                 "\n- Arsenal adding extra mag when no 3CB weapon swap available [(#679)](https://github.com/uksf/modpack/issues/679)" +
//                                 "\n- Backpack-on-chest causing weapons and backpacks to be deleted [(#688)](https://github.com/uksf/modpack/issues/688)" +
//                                 "\n- Drone init not running for correct classname" +
//                                 "\n- Husky vanilla logistics values (removed them) [(#681)](https://github.com/uksf/modpack/issues/681)" +
//                                 "\n\n#### Updated" +
//                                 "\n- ACRE to [2.7.4.1027 + Dev](https://github.com/uksf/modpack/issues/691)" +
//                                 "\n- Lambs to [2.4.4](https://github.com/uksf/modpack/issues/690)" +
//                                 "\n- ZEN to [1.8.0](https://github.com/uksf/modpack/issues/689)" +
//                                 "\n\n[Report and track issues here](https://github.com/uksf/modpack/issues)\n"
//                 }
//             );
//
//             IBuildsService buildsService = serviceProvider.GetService<IBuildsService>();
//             buildsService.Data.Add(
//                 new ModpackBuildRelease {
//                     id = ObjectId.GenerateNewId().ToString(),
//                     version = "5.17.16",
//                     builds = new List<ModpackBuild> {
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-14),
//                             buildNumber = 0,
//                             pushEvent = new GithubPushEvent { commit = new GithubCommit { message = "New version" } },
//                             isNewVersion = true
//                         },
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-16).AddHours(-2),
//                             buildNumber = 1,
//                             pushEvent = new GithubPushEvent {
//                                 commit = new GithubCommit { message = "Changed captive escort to be local to unit" + "\n\n- Exit escort if weapon holstered (can't get anims right)" }
//                             }
//                         },
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-16),
//                             buildNumber = 2,
//                             pushEvent = new GithubPushEvent { commit = new GithubCommit { message = "Fix missing getPos for zeus fps" } }
//                         },
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-15).AddHours(-1),
//                             buildNumber = 3,
//                             pushEvent = new GithubPushEvent {
//                                 commit = new GithubCommit {
//                                     message = "Add name abbreviations to resupply crates" +
//                                               "\n\n- Add coded name abbreviations (e.g  (AM) = Ammo Mixed) to resupply crates" +
//                                               "\n- Make identifying in-game easier"
//                                 }
//                             }
//                         },
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-15),
//                             buildNumber = 4,
//                             isReleaseCandidate = true,
//                             pushEvent = new GithubPushEvent { commit = new GithubCommit { message = "Tweak zeus player fps display" } }
//                         }
//                     }
//                 }
//             );
//             buildsService.Data.Add(
//                 new ModpackBuildRelease {
//                     id = ObjectId.GenerateNewId().ToString(),
//                     version = "5.17.17",
//                     builds = new List<ModpackBuild> {
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-14),
//                             buildNumber = 0,
//                             pushEvent = new GithubPushEvent { commit = new GithubCommit { message = "New version" } },
//                             isNewVersion = true
//                         },
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-11).AddHours(-2),
//                             buildNumber = 1,
//                             pushEvent = new GithubPushEvent {
//                                 commit = new GithubCommit { message = "Changed captive escort to be local to unit" + "\n\n- Exit escort if weapon holstered (can't get anims right)" }
//                             }
//                         },
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-11),
//                             buildNumber = 2,
//                             pushEvent = new GithubPushEvent { commit = new GithubCommit { message = "Fix missing getPos for zeus fps" } }
//                         },
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-10).AddHours(-1),
//                             buildNumber = 3,
//                             pushEvent = new GithubPushEvent {
//                                 commit = new GithubCommit {
//                                     message = "Add name abbreviations to resupply crates" +
//                                               "\n\n- Add coded name abbreviations (e.g  (AM) = Ammo Mixed) to resupply crates" +
//                                               "\n- Make identifying in-game easier"
//                                 }
//                             }
//                         },
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-10),
//                             buildNumber = 4,
//                             pushEvent = new GithubPushEvent { commit = new GithubCommit { message = "Tweak zeus player fps display" } }
//                         },
//                         new ModpackBuild {
//                             timestamp = DateTime.Now.AddDays(-9),
//                             buildNumber = 5,
//                             pushEvent = new GithubPushEvent {
//                                 commit = new GithubCommit {
//                                     message = "Fixed drone interactions" + "\n\n- Functionality was missing from current drone" + "\n- Changed interactions to script added"
//                                 }
//                             }
//                         }
//                     }
//                 }
//             );
//         }
//     }
// }


