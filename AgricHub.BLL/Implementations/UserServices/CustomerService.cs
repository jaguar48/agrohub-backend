using AgricHub.BLL.Helpers;
using AgricHub.BLL.Interfaces.IUserServices;
using AgricHub.Contracts;
using AgricHub.DAL.Entities;
using AgricHub.DAL.Entities.Models;
using AgricHub.Shared.DTO_s.Request;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace AgricHub.BLL.Implementations.UserServices
{
    public sealed class CustomerService : ICustomerService
    {
        private readonly IRepository<Customer> _customerRepo;
        private readonly IRepository<Wallet> _walletRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserServices _userServices;
        private readonly IAuthService _authService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerService(
            IAuthService authService,
            IUnitOfWork unitOfWork,
            IUserServices userServices,
            UserManager<ApplicationUser> userManager)
        {
            _authService = authService;
            _unitOfWork = unitOfWork;
            _userServices = userServices;
            _userManager = userManager;
            _customerRepo = unitOfWork.GetRepository<Customer>();
            _walletRepo = unitOfWork.GetRepository<Wallet>();
        }

        public async Task<string> RegisterCustomer(CustomerRegistrationRequest request)
        {
            ApplicationUser user = null;

            try
            {
                // 🔒 START TRANSACTION
                await _unitOfWork.BeginTransactionAsync();

                // 1. Register user in Identity
                user = await _userServices.RegisterUser(new UserForRegistrationRequest
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    Password = request.Password,
                    UserName = request.UserName,
                    CountryId = request.CountryId,
                    StateId = request.StateId,
                    Address = request.Address,
                    PhoneNumber = request.PhoneNumber
                });

                // 2. Add Customer role
                await _userManager.AddToRoleAsync(user, "Customer");

                // 3. Create Customer record
                var customer = new Customer
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    Address = request.Address,
                    CountryId = request.CountryId,
                    StateId = request.StateId,
                    UserId = user.Id,
                    SendbirdChannelUrl = null  // Will be set later when creating chat
                };

                await _customerRepo.AddAsync(customer);
                await _unitOfWork.SaveChangesAsync(); // Save to get customer.Id

                // 4. Create Wallet for Customer
                await CreateWalletForCustomer(customer);
                await _unitOfWork.SaveChangesAsync();

                // ✅ All successful - commit transaction
                await _unitOfWork.CommitTransactionAsync();

                var result = new
                {
                    success = true,
                    message = "Registration successful! Please check your email for verification."
                };
                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                // ❌ Rollback transaction
                await _unitOfWork.RollbackTransactionAsync();

                // Clean up user if created (compensating action for Identity)
                if (user != null)
                {
                    try
                    {
                        await _userManager.DeleteAsync(user);
                    }
                    catch
                    {
                        // Log failure to delete user
                    }
                }

                var result = new
                {
                    success = false,
                    message = $"Registration failed: {ex.Message}"
                };
                return JsonConvert.SerializeObject(result);
            }
        }

        private async Task CreateWalletForCustomer(Customer customer)
        {
            Wallet wallet = new()
            {
                WalletNo = WalletIdGenerator.GenerateWalletId(),
                Balance = 0,
                IsActive = true,
                CustomerId = customer.Id
            };
            await _walletRepo.AddAsync(wallet);
        }
    }
}