module App.State

open System
open Elmish
open Elmish.Browser.UrlParser
open Elmish.Browser.Navigation
open App.Types
open Shared
open Fable.Import.Browser
open Fable

type BackofficePage = Admin.Backoffice.Types.Page
type PostsPage = Posts.Types.Page

let pageHash = function 
    | Page.About -> Urls.about 
    | Page.Posts page -> 
        match page with 
        | Posts.Types.Page.AllPosts -> Urls.posts
        | Posts.Types.Page.Post postSlug -> Urls.combine [ Urls.posts; postSlug ]   
    | Page.Admin adminPage ->
        match adminPage with 
        | Admin.Types.Page.Login -> Urls.login
        | Admin.Types.Page.Backoffice backofficePage ->
            match backofficePage with 
            | BackofficePage.Home -> Urls.admin
            | BackofficePage.NewPost -> Urls.combine [ Urls.admin; Urls.newPost ]   
            | BackofficePage.Drafts -> Urls.combine [ Urls.drafts; Urls.drafts ]  
            | BackofficePage.PublishedPosts -> Urls.combine [ Urls.admin; Urls.publishedPosts ]
            | BackofficePage.Settings -> Urls.combine [ Urls.admin; Urls.settings ]
            | BackofficePage.EditArticle postId -> Urls.combine [ Urls.admin; Urls.editArticle; string postId ]

let pageParser: Parser<Page -> Page, Page> =
  oneOf [ map About (s Urls.about)
          map (Admin Admin.Types.Page.Login) (s Urls.login)
          map (PostsPage.Post >> Posts) (s Urls.posts </> str)
          map (Posts PostsPage.AllPosts) (s Urls.posts )
          map (Admin (Admin.Types.Page.Backoffice BackofficePage.Home)) (s Urls.admin)
          map (Admin (Admin.Types.Page.Backoffice BackofficePage.NewPost)) (s Urls.admin </> s Urls.newPost)
          map (fun id -> Admin (Admin.Types.Page.Backoffice (BackofficePage.EditArticle id))) (s Urls.admin </> s Urls.editArticle </> i32)
          map (Admin (Admin.Types.Page.Backoffice BackofficePage.Drafts)) (s Urls.admin </> s Urls.drafts)
          map (Admin (Admin.Types.Page.Backoffice BackofficePage.PublishedPosts)) (s Urls.admin </> s Urls.publishedPosts)
          map (Admin (Admin.Types.Page.Backoffice BackofficePage.Settings)) (s Urls.admin </> s Urls.settings) ]


/// Tries to parse a url into a page 
let parseUrl (urlHash: string) = 
    let segments = 
        urlHash.Substring(1, urlHash.Length - 1) // remove the hash sign
        |> fun hash -> hash.Split '/'
        |> List.ofArray
        |> List.filter (String.IsNullOrWhiteSpace >> not)  

    match segments with
    | [ Urls.about ] -> 
        // the about page
        App.Types.Page.About
        |> Some 

    | [ Urls.posts ] -> 
        // all posts page
        Posts.Types.Page.AllPosts
        |> App.Types.Page.Posts
        |> Some

    | [ Urls.posts; postSlug ] -> 
        // matches against a specific post by it's slug
        Posts.Types.Page.Post postSlug
        |> App.Types.Page.Posts
        |> Some  
    
    | [ Urls.admin ] -> 
        // the home page of the backoffice
        Admin.Backoffice.Types.Page.Home
        |> Admin.Types.Page.Backoffice
        |> App.Types.Page.Admin
        |> Some

    | [ Urls.admin; Urls.login ] -> 
        // the login page 
        Admin.Types.Page.Login
        |> App.Types.Page.Admin
        |> Some 

    | [ Urls.admin; Urls.drafts ] ->
        // the drafts page 
        Admin.Backoffice.Types.Page.Drafts
        |> Admin.Types.Page.Backoffice
        |> App.Types.Page.Admin
        |> Some 

    | [ Urls.admin; Urls.publishedPosts ] ->
        // the page of published stories
        Admin.Backoffice.Types.Page.PublishedPosts
        |> Admin.Types.Page.Backoffice
        |> App.Types.Page.Admin
        |> Some 

    | [ Urls.admin; Urls.newPost ] ->
        // the new post page
        Admin.Backoffice.Types.Page.NewPost
        |> Admin.Types.Page.Backoffice
        |> App.Types.Page.Admin
        |> Some 
    
    | [ Urls.admin; Urls.settings ] ->
        // the settings page
        Admin.Backoffice.Types.Page.Settings
        |> Admin.Types.Page.Backoffice
        |> App.Types.Page.Admin
        |> Some 

    | [ Urls.admin; Urls.editArticle; Urls.Int postId ] ->
        // editing a post by the post id 
        Admin.Backoffice.Types.Page.EditArticle postId 
        |> Admin.Types.Page.Backoffice
        |> App.Types.Page.Admin
        |> Some

    | _ -> None 


let init() =
  let posts, postsCmd = Posts.State.init()
  let admin, adminCmd = Admin.State.init()
  let model =
      { BlogInfo = Empty
        CurrentPage = None
        Admin = admin
        Posts = posts }

  let initialPageCmd = 
    match parseUrl window.location.hash with  
    | Some page -> Cmd.ofMsg (UrlUpdated page)
    | None -> 
        Posts.Types.Page.AllPosts
        |> App.Types.Page.Posts
        |> UrlUpdated
        |> Cmd.ofMsg 


  model, Cmd.batch [ initialPageCmd
                     Cmd.map PostsMsg postsCmd
                     Cmd.map AdminMsg adminCmd
                     Cmd.ofMsg LoadBlogInfo ]

let showInfo msg = 
    Toastr.message msg
    |> Toastr.withTitle "Tabula Rasa"
    |> Toastr.info  

let update msg state =
  match msg with
  | PostsMsg msg ->
      let postsState, postsCmd = Posts.State.update state.Posts msg 
      let appState = { state with Posts = postsState }
      let appCmd = Cmd.map PostsMsg postsCmd
      appState, appCmd

  | AdminMsg (Admin.Types.BackofficeMsg (Admin.Backoffice.Types.SettingsMsg ((Admin.Backoffice.Settings.Types.ChangesSaved msg)))) ->
        state, Cmd.ofMsg LoadBlogInfo
  
  | AdminMsg msg ->
      let nextAdminState, adminCmd = Admin.State.update msg state.Admin
      let nextAppState = { state with Admin = nextAdminState }
      let nextAppCmd = Cmd.map AdminMsg adminCmd
      nextAppState, nextAppCmd
      
  | LoadBlogInfo ->
      let nextState = { state with BlogInfo = Loading }
      nextState, Http.loadBlogInfo
      
  | BlogInfoLoaded (Ok blogInfo) ->
      let nextState = { state with BlogInfo = Body blogInfo }
      let setPageTitle title = 
        Fable.Import.Browser.document.title <- title 
      nextState, Cmd.attemptFunc setPageTitle blogInfo.BlogTitle (fun ex -> DoNothing)
      
  | BlogInfoLoaded (Error errorMsg) ->
     let nextState = { state with BlogInfo = LoadError errorMsg }
     nextState, Toastr.error (Toastr.message errorMsg)

  | BlogInfoLoadFailed msg ->
      let nextState = { state with BlogInfo = LoadError msg }
      nextState, Cmd.none
      
  | NavigateTo page ->
      let nextUrl = Urls.hashPrefix (pageHash page)
      state, Urls.newUrl nextUrl

  | DoNothing ->
      state, Cmd.none
      
  | UrlUpdated page -> 
      match page with 
      | Page.Posts page -> 
           // make sure to load posts anytime the posts page is requested
           let nextAppState = { state with CurrentPage = Some (Posts page) }
           let nextCmd =
              match page with
              | Posts.Types.Page.AllPosts  -> Cmd.ofMsg (PostsMsg Posts.Types.Msg.LoadLatestPosts)
              | Posts.Types.Page.Post slug -> Cmd.ofMsg (PostsMsg (Posts.Types.Msg.LoadSinglePost slug))
              
           nextAppState, nextCmd

      | Page.About ->
           let nextState = { state with CurrentPage = Some Page.About }
           nextState, Cmd.none

      | Page.Admin adminPage ->
        let nextAdminCmd = 
          match adminPage with
          | Admin.Types.Page.Login ->
              match state.Admin.SecurityToken with
              | None -> Cmd.none
              | Some _ -> Cmd.batch [ Urls.navigate [ Urls.admin ];
                                      showInfo "Already logged in" ]
     
          | Admin.Types.Page.Backoffice _ ->
              match state.Admin.SecurityToken with
              | None -> Cmd.batch [ Urls.navigate [ Urls.login ]
                                    showInfo "You must be logged in first" ]
              | Some _ -> Cmd.none

        let nextState = { state with CurrentPage = Some (Admin adminPage) }
        nextState, nextAdminCmd
