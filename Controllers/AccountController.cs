using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviesMafia.Models.GenericRepo;
using MoviesMafia.Models.Repo;
using System.Security.Claims;

namespace MoviesMafia.Controllers
{

    public class AccountController : Controller
    {
        private readonly IUserRepo _userRepo;
        private readonly IRecordsRepo _recordsRepo;
        public AccountController(IUserRepo userRepo, IRecordsRepo recordsRepo)
        {
            _userRepo = userRepo;
            _recordsRepo = recordsRepo;
        }

        [HttpPost]
        public async Task<IActionResult> SignUp(UserSignUpModel model)
        {

            if (ModelState.IsValid)
            {

                var result = await _userRepo.SignUpAsync(model);
                if (result.Succeeded)
                {
                    object data = "A Confirmation Email Has Been Sent To Your Email Address. Please Check Your Email And Click The Confirmation Link To Activate Your Account.";
                    return View("ThankYou", data);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> LogIn(UserLogInModel model, string returnUrl = null)
        {

            if (ModelState.IsValid)
            {
                var result = await _userRepo.LogInAsync(model);
                if (result.IsNotAllowed)
                {
                    ModelState.AddModelError(string.Empty, "In Order To Login You Need To Confirm Your Email. Check Your Email Box.");

                }
                else
                {
                    if (result.Succeeded)
                    {
                        if (!string.IsNullOrEmpty(returnUrl))
                        {
                            return LocalRedirect(returnUrl);
                        }
                        return RedirectToAction("GetMovie", "Collection");
                    }
                    ModelState.AddModelError(string.Empty, "Invalid Username or Password");

                }
            }
            else
            {
                var result = await _userRepo.LogInAsync(model);
                if (result.IsNotAllowed)
                {
                    ModelState.AddModelError(string.Empty, "In Order To Login You Need To Confirm Your Email. Check Your Email Box.");

                }
                else
                {
                    if (result.Succeeded)
                    {
                        return RedirectToAction("GetMovie", "Collection");
                    }
                    ModelState.AddModelError(string.Empty, "Invalid Username or Password");
                }
            }
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _userRepo.LogoutAsync();
            return RedirectToAction("GetMovie", "Collection");
        }
        [HttpGet]
        public async Task<IActionResult> Login()
        {
            if (ModelState.IsValid)
            {
                await _userRepo.LogoutAsync();
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SignUp()
        {

            if (ModelState.IsValid)
            {
                await _userRepo.LogoutAsync();
            }
            return View();
        }
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            ViewBag.EmailData = await _userRepo.GetUserEmailAsync(User.Identity.Name);
            var ProfilePicturePath = await _userRepo.GetUserProfilePictureAsync(User.Identity.Name);
            var extension = Path.GetExtension(ProfilePicturePath);
            ViewBag.ProfilePicturePath = User.Identity.Name + extension;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var list = await _recordsRepo.GetRecordsByUserIdAsync(userId);
            var tuple = new Tuple<List<Records>, UpdateUserModel>(list, new UpdateUserModel());

            return View(tuple);
        }

        [Authorize]
        public async Task<IActionResult> DeleteRecord(string deleteButton)
        {
            ViewBag.EmailData = await _userRepo.GetUserEmailAsync(User.Identity.Name);
            Records del = System.Text.Json.JsonSerializer.Deserialize<Records>(deleteButton);

            _recordsRepo.Delete(del);
            await _recordsRepo.SaveChangesAsync();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var list = await _recordsRepo.GetRecordsByUserIdAsync(userId);
            return RedirectToAction("Profile", list);
        }

        [Authorize]
        public async Task<IActionResult> RequestMovie(string name, int year, string type)
        {
            object data = string.Empty;
            string cookieNameMovie = "movie_name_" + HttpContext.User.Identity.Name;
            if (HttpContext.Request.Cookies.ContainsKey(cookieNameMovie))
            {
                data = "You Already Have Requested A Movie/Season Try Again After 24 Hours";
            }
            else
            {
                try
                {

                    _recordsRepo.Add(new Records { Name = name, UserId = User.FindFirstValue(ClaimTypes.NameIdentifier), Year = year, Type = type });
                    await _recordsRepo.SaveChangesAsync();
                    data = "Your Requested Movie " + name + " Has Been Received";
                    HttpContext.Response.Cookies.Append(cookieNameMovie, name, new CookieOptions { Expires = DateTime.Now.AddDays(1) });
                }
                catch (Exception ex)
                {
                    data = ex.Message;
                }
            }
            return View("ThankYou", data);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> EditRecord(int id, string name, int year, string type)
        {
            if (ModelState.IsValid)
            {

                Records r = _recordsRepo.GetById(id);
                r.Name = name; r.Year = year; r.Type = type; r.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _recordsRepo.Update(r);
                await _recordsRepo.SaveChangesAsync();
            }
            return RedirectToAction("Profile");
        }
        [Authorize]
        public IActionResult EditRecord(int id)
        {
            Records r = _recordsRepo.GetById(id);
            return View("EditRecord", r);
        }
        [Authorize]
        [HttpPost]
        public async Task<string> UpdatePassword(UpdateUserModel model)
        {

            if (ModelState.IsValid)
            {
                var user = await _userRepo.GetUserAsync(User);
                var updateResult = await _userRepo.UpdatePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (updateResult.Succeeded)
                {
                    return "Password Updated Successfully";
                }
                else
                {
                    return "Current Password Is Incorrect";
                }
            }
            else
            {
                return "New Password and Confirm Password Do Not Match";
            }
        }
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin()
        {
            var allUsers = await _userRepo.GetAllUsersAsync();
            return View(allUsers);
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string deleteButton)
        {
            AppUser del = System.Text.Json.JsonSerializer.Deserialize<AppUser>(deleteButton);
            var result = await _userRepo.DeleteUserAsync(del);
            var list = await _userRepo.GetAllUsersAsync();
            return RedirectToAction("Admin", list);
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditUser(string id)
        {
            var reult = await _userRepo.GetUserByIdAsync(id);
            return View("EditUser", reult);
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateEmail(string id, string email)
        {
            var result = await _userRepo.UpdateEmailAsync(id, email);
            return RedirectToAction("Admin");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return BadRequest("Invalid Email Or Token.");
            }

            var result = await _userRepo.VerifyEmailAsync(email, token);

            if (result)
            {
                object data = "Email Confirmed";
                return View("ThankYou", data);
            }
            else
            {
                object data = "Error Confirming Your Email Address.";
                return View("ThankYou", data);

            }
        }

        [Authorize]
        public async Task<IActionResult> UpdateProfilePicture(List<IFormFile> updatedProfilePicture)
        {
            if (updatedProfilePicture.Count == 0)
            {
                return BadRequest("Error");
            }
            if (ModelState.IsValid)
            {
                var result = await _userRepo.UpdateProfilePictureAsync(updatedProfilePicture[0], User.Identity.Name);
                if (result)
                {
                    return Content("Profile Picture Updated Successfully");
                }
                else
                {
                    return Content("Error While Updating The Profile Picture");
                }
            }
            else
            {
                return Content("Error While Updating The Profile Picture");
            }
        }

    }
}
