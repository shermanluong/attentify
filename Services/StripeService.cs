using GoogleLogin.Models;

namespace GoogleLogin.Services
{
    public class StripeService
    {
        private readonly IServiceScopeFactory       _serviceScopeFactory;
        private readonly ILogger<ShopifyService>    _logger;
        private readonly IConfiguration             _configuration;
		public StripeService(
            IServiceScopeFactory serviceScopeFactory, 
            IConfiguration configuration, 
            ILogger<ShopifyService> logger)
        {
            _serviceScopeFactory    = serviceScopeFactory;
            _logger                 = logger;
            _configuration          = configuration;
        }

        public long addPlan(string planName, int planLevel, string priceId)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbPlans
                        .Where(e => e.planName == planName)
                        .FirstOrDefault();

                    if (_one != null)
                    {
                        return -1;
                    }

                    var newPlan = new TbPlan
                    {
                        planName = planName,
                        planLevel = planLevel,
                        priceId = priceId
                    };

                    _dbContext.Add(newPlan);
                    _dbContext.SaveChanges();

                    return newPlan.id;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return -1;
        }

        public List<TbPlan> getPlans()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                var planList = _dbContext.TbPlans.ToList();
                return planList;
            }
        }

        public TbPlan? getPlanByPriceId(string priceId)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbPlans
                        .Where(e => e.priceId == priceId)
                        .FirstOrDefault();

                    return _one;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return null;
        }

        public TbPlan? getPlanById(long id)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbPlans
                        .Where(e => e.id == id)
                        .FirstOrDefault();

                    return _one;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return null;
        }

        public TbCompany? getCompanyByIdx(long companyIdx)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbCompanies
                        .Where(e => e.id == companyIdx)
                        .FirstOrDefault();

                    return _one;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return null;
        }

        public int deleteCompany(TbCompany company)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                _dbContext.TbCompanies.Remove(company);
                _dbContext.SaveChanges();

                return 1;
            }
        }

        public long addUserPlan(string userEmail, long planId, long expire)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbUserPlans
                        .Where(e => e.userEmail == userEmail)
                        .FirstOrDefault();

                    if (_one != null)
                    {
                        _one.planId = planId;
                        _one.expire = expire;
                    } else {
                        var newUserPlan = new TbUserPlan
                        {
                            userEmail = userEmail,
                            planId = planId,
                            expire = expire
                        };

                        _dbContext.Add(newUserPlan);
                    }
                    _dbContext.SaveChanges();

                    return 1;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return -1;
        }

        public TbUserPlan? getUserPlan(string userEmail)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                try
                {
                    var _one = _dbContext
                        .TbUserPlans
                        .Where(e => e.userEmail == userEmail)
                        .FirstOrDefault();

                    return _one;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }

            return null;
        }

        public UserPlanDetail? getUserPlanDetail(string userEmail)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

                var userPlanDetail = (from userPlan in _dbContext.TbUserPlans
                                   join plan in _dbContext.TbPlans
                                   on userPlan.planId equals plan.id
                                   where userPlan.userEmail == userEmail
                                   select new UserPlanDetail
                                   {
                                       id = userPlan.id,
                                       userEmail = userPlan.userEmail,
                                       planName = plan.planName,
                                       planLevel = plan.planLevel,
                                       priceId = plan.priceId,
                                       expire = userPlan.expire
                                   }).FirstOrDefault();

                return userPlanDetail;
            }
        }
    }
}
