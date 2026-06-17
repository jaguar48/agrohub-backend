using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.DAL.Entities;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Identity;

namespace AgricHub.BLL.Implementations.UserServices
{
    public sealed class UserService : IUserServices
    {

        /*private readonly ILoggerManager _logger;*/
        private readonly UserManager<ApplicationUser> _userManager;



        public UserService(UserManager<ApplicationUser> userManager)
        {
            /*_logger = logger;*/
            _userManager = userManager;
        }



        public async Task<ApplicationUser> RegisterUser(UserForRegistrationRequest Request)
        {

            /*_logger.LogInfo("Checking if user exist, if not create the user.");*/
            var existingUser = await _userManager.FindByEmailAsync(Request.Email.Trim().ToLower());
            if (existingUser != null)
            {
                throw new InvalidOperationException("Email exists!");
            }

            var user = new ApplicationUser
            {
                FirstName = Request.FirstName,
                LastName = Request.LastName,
                UserName = Request.UserName,
                Email = Request.Email,
                PhoneNumber = Request.PhoneNumber,
                CountryId = Request.CountryId,
                StateId = Request.StateId,
                Address = Request.Address

            };



            var result = await _userManager.CreateAsync(user, Request.Password);
            if (!result.Succeeded)
            {

                string errMsg = string.Join("\n", result.Errors.Select(x => x.Description));

                throw new InvalidOperationException($"Failed to create user:\n{errMsg}");
            }

            return user;

        }


    }
}
