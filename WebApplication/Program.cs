using Microsoft.AspNetCore.Mvc;
using SSOReady.Client;

// Do not hard-code or leak your SSOReady API key in production!
//
// In production, instead you should configure a secret SSOREADY_API_KEY
// environment variable. The SSOReady SDK automatically loads an API key from
// SSOREADY_API_KEY.
//
// This key is hard-coded here for the convenience of logging into a test app,
// which is hard-coded to run on http://localhost:5293. It's only because of
// this very specific set of constraints that it's acceptable to hard-code and
// publicly leak this API key.
var ssoready = new SSOReady.Client.SSOReady("ssoready_sk_cw96rvovfz2wtcko8cj771nqq");

var builder = WebApplication.CreateBuilder(args);

// This demo uses HTTPContext.Session to do user sessions, to keep things
// simple. SSOReady works with any stack or session technology you use.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// This demo just renders plain old HTML with no client-side JavaScript. This is
// only to keep the demo minimal. SSOReady works with any frontend stack or
// framework you use.
//
// This demo keeps the HTML minimal to keep things as simple as possible here.
app.MapGet("/", (HttpContext context) =>
{
    return Results.Content($"""
    <!doctype html>
    <html>
        <body>
            <h1>Hello, {context.Session.GetString("email") ?? "logged-out user"}!</h1>

            <a type="button" href="/logout">Log out</a>

            <!-- submitting this form makes the user's browser do a GET /saml-redirect?email=... -->
            <form method="get" action="/saml-redirect">
                <h2>Log in with SAML</h2>

                <label for="email">Email</label>
                <input id="email" type="email" name="email" placeholder="john.doe@example.com" />

                <button>Submit</button>

                <p>(Try any @example.com or @example.org email address.)</p>
            </form>
        </body>
    </html>
    """, "text/html");
});

// This is the page users visit when they click on the "Log out" link in this
// demo app. It just resets `email` in context.Session, which deletes the user's
// session cookie in this demo.
//
// SSOReady doesn't impose any constraints on how your app's sessions work.
app.MapGet("/logout", (HttpContext context) =>
{
    context.Session.Remove("email");
    return Results.Redirect("/");
});

// This is the page users visit when they submit the "Log in with SAML" form in
// this demo app.
app.MapGet("/saml-redirect", async (string email) =>
{
    // To start a SAML login, you need to redirect your user to their employer's
    // particular Identity Provider. This is called "initiating" the SAML login.
    //
    // Use `GetSamlRedirectUrlAsync` to initiate a SAML login.
    var redirectResponse = await ssoready.Saml.GetSamlRedirectUrlAsync(new GetSamlRedirectUrlRequest
    {
        // OrganizationExternalId is how you tell SSOReady which company's
        // identity provider you want to redirect to.
        //
        // In this demo, we identify companies using their domain. This code
        // converts "john.doe@example.com" into "example.com".
        OrganizationExternalId = email.Split("@")[1]
    });

    // `GetSamlRedirectUrlAsync` returns an object like this:
    //
    // { RedirectUrl = "https://..." }
    //
    // To initiate a SAML login, you redirect the user to that RedirectUrl.
    return Results.Redirect(redirectResponse.RedirectUrl!);
});

// This is the page SSOReady redirects your users to when they've successfully
// logged in with SAML.
app.MapGet("/ssoready-callback", async (HttpContext context, [FromQuery(Name = "saml_access_code")] string samlAccessCode) =>
{
    // SSOReady gives you a one-time SAML access code under
    // ?saml_access_code=saml_access_code_... in the callback URL's query
    // parameters.
    //
    // You redeem that SAML access code using `RedeemSamlAccessCodeAsync`, which
    // gives you back the user's email address. Then, it's your job to log the
    // user in as that email.
    var redeemResponse = await ssoready.Saml.RedeemSamlAccessCodeAsync(new RedeemSamlAccessCodeRequest
    {
        SamlAccessCode = samlAccessCode
    });

    // SSOReady works with any stack or session technology you use. In this demo
    // app, we use HTTPContext.Session. This code is how HTTPContext.Session
    // does logins.
    context.Session.SetString("email", redeemResponse.Email!);

    // Redirect back to the demo app homepage.
    return Results.Redirect("/");
});

app.UseSession();

app.Run();
