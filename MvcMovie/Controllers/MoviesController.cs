using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MvcMovie.Models;

using MongoDB.Azure;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace MvcMovie.Controllers
{ 
    public class MoviesController : Controller
    {
        private MongoCollection<Movie> GetMoviesCollection()
        {
            var server = MongoHelper.GetMongoServer();
            var database = server["movies"];
            var movieCollection = database.GetCollection<Movie>("movies");
            return movieCollection;
        }

        //
        // GET: /Movies/

        public ViewResult Index()
        {
            var collection = GetMoviesCollection();
            var query = Query.Null;
            var cursor = collection.Find(query);
            return View(cursor.ToList<Movie>());
        }

        //
        // GET: /Movies/Details/5

        public ViewResult Details(string id)
        {
            var collection = GetMoviesCollection();
            var query = Query.EQ("_id", new ObjectId(id));
            var movie = collection.FindOneAs<Movie>(query);
            return View(movie);
        }

        //
        // GET: /Movies/Create

        public ActionResult Create()
        {
            return View();
        } 

        //
        // POST: /Movies/Create

        [HttpPost]
        public ActionResult Create(Movie movie)
        {
            if (ModelState.IsValid)
            {
                var collection = GetMoviesCollection();
                collection.Insert<Movie>(movie);
                return RedirectToAction("Index");  
            }

            return View(movie);
        }
        
        //
        // GET: /Movies/Edit/5
 
        public ActionResult Edit(string id)
        {
            var collection = GetMoviesCollection();
            var query = Query.EQ("_id", new ObjectId(id));
            var movie = collection.FindOneAs<Movie>(query);
            return View(movie);
        }

        //
        // POST: /Movies/Edit/5

        [HttpPost]
        public ActionResult Edit(Movie movie)
        {
            if (ModelState.IsValid)
            {
                var collection = GetMoviesCollection();
                collection.Save<Movie>(movie);
                return RedirectToAction("Index");
            }
            return View(movie);
        }

        //
        // GET: /Movies/Delete/5
 
        public ActionResult Delete(string id)
        {
            var collection = GetMoviesCollection();
            var query = Query.EQ("_id", new ObjectId(id));
            var movie = collection.FindOneAs<Movie>(query);
            return View(movie);
        }

        //
        // POST: /Movies/Delete/5

        [HttpPost, ActionName("Delete")]
        public ActionResult DeleteConfirmed(string id)
        {
            var collection = GetMoviesCollection();
            var query = Query.EQ("_id", new ObjectId(id));
            var result = collection.Remove(query);
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            // db.Dispose();
            base.Dispose(disposing);
        }

        //
        // GET: /Movies/About

        public ActionResult About()
        {
            return View();
        }

    }
}