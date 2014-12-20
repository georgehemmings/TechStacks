﻿using System;
using System.Linq;
using ServiceStack;
using ServiceStack.Configuration;
using ServiceStack.OrmLite;
using TechStacks.ServiceModel;
using TechStacks.ServiceModel.Types;

namespace TechStacks.ServiceInterface
{
    [Authenticate(ApplyTo = ApplyTo.Put | ApplyTo.Post | ApplyTo.Delete)]
    public class TechnologyServices : Service
    {
        public object Post(CreateTechnology request)
        {
            var tech = request.ConvertTo<Technology>();
            var session = SessionAs<AuthUserSession>();
            tech.CreatedBy = session.UserName;
            tech.LastModifiedBy = session.UserName;
            tech.LastModified = DateTime.UtcNow;
            tech.Created = DateTime.UtcNow;
            tech.OwnerId = session.UserAuthId;

            // disable explicit approval until we first have a problem
            //if (!session.HasRole(RoleNames.Admin))
            //{
            //    tech.LogoApproved = false;
            //}
            tech.LogoApproved = true;

            tech.SlugTitle = tech.Name.GenerateSlug();
            var id = Db.Insert(tech, selectIdentity: true);
            var createdTechStack = Db.SingleById<Technology>(id);

            var history = createdTechStack.ConvertTo<TechnologyHistory>();
            history.TechnologyId = id;
            history.Operation = "INSERT";
            Db.Insert(history);

            return new CreateTechnologyResponse
            {
                Tech = createdTechStack
            };
        }

        public object Put(UpdateTechnology request)
        {
            var existingTech = Db.SingleById<Technology>(request.Id);
            if (existingTech == null)
                throw HttpError.NotFound("Tech not found");

            var session = SessionAs<AuthUserSession>();
            
            // disable explicit approval until we first have a problem
            //if (request.LogoUrl != existingTech.LogoUrl && !session.HasRole(RoleNames.Admin))
            //{
            //    existingTech.LogoApproved = false;
            //}
            if (existingTech.IsLocked && !session.HasRole(RoleNames.Admin))
                throw HttpError.Unauthorized("Technology changes are currently restricted to Administrators only.");

            var updated = request.ConvertTo<Technology>();
            //Carry over current logo approved status and locked status
            updated.LogoApproved = existingTech.LogoApproved;
            updated.IsLocked = existingTech.IsLocked;
            updated.LastModifiedBy = session.UserName;
            updated.LastModified = DateTime.UtcNow;
            updated.OwnerId = existingTech.OwnerId;
            updated.CreatedBy = existingTech.CreatedBy;

            //Update SlugTitle
            updated.SlugTitle = updated.Name.GenerateSlug();
            Db.Save(updated);

            var history = updated.ConvertTo<TechnologyHistory>();
            history.TechnologyId = updated.Id;
            history.Operation = "UPDATE";
            Db.Insert(history);

            return new UpdateTechnologyResponse
            {
                Tech = updated
            };
        }

        public object Delete(DeleteTechnology request)
        {
            var existingTech = Db.SingleById<Technology>(request.Id);
            if (existingTech == null)
                throw HttpError.NotFound("Tech not found");

            var session = SessionAs<AuthUserSession>();
            if (existingTech.OwnerId != session.UserAuthId && !session.HasRole(RoleNames.Admin))
                throw HttpError.Unauthorized("You are not the owner of this technology.");

            Db.DeleteById<Technology>(request.Id);

            var history = existingTech.ConvertTo<TechnologyHistory>();
            history.TechnologyId = existingTech.Id;
            history.LastModified = DateTime.UtcNow;
            history.LastModifiedBy = session.UserName;
            history.Operation = "DELETE";
            Db.Insert(history);

            return new DeleteTechnologyResponse
            {
                Tech = new Technology { Id = (long)request.Id }
            };
        }

        public object Get(Technologies request)
        {
            int id;
            var tech = int.TryParse(request.Slug, out id)
                ? Db.SingleById<Technology>(id)
                : Db.Single<Technology>(x => x.SlugTitle == request.Slug.ToLower());

            if (tech == null)
                HttpError.NotFound("Tech stack not found");

            return new TechnologiesResponse {
                Tech = tech
            };
        }

        public object Get(AllTechnologies request)
        {
            return new AllTechnologiesResponse
            {
                Techs = Db.Select(Db.From<Technology>().Take(100)).ToList()
            };
        }

        public object Get(GetStacksThatUseTech request)
        {
            var stacksByTech = Db.Select(Db.From<TechnologyStack>()
                .Join<TechnologyChoice, TechnologyStack>(
                    (techChoice, techStack) => techChoice.TechnologyId == request.Id && techChoice.TechnologyStackId == techStack.Id)
                .SelectDistinct(x => x.Id));

            return new GetStacksThatUseTechResponse
            {
                TechStacks = stacksByTech.ToList()
            };
        }
    }
}
