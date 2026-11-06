using LMS.Data;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(LMS.Startup))]
namespace LMS
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);

            // Execute database seeder on application startup
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    DbSeeder.Seed(context);
                }
            }
            catch
            {
                // Ensure startup doesn't fail if seeding encounters temporary DB locks
            }
        }
    }
}
