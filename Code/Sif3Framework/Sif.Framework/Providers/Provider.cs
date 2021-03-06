﻿/*
 * Copyright 2017 Systemic Pty Ltd
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Sif.Framework.Extensions;
using Sif.Framework.Model.DataModels;
using Sif.Framework.Model.Exceptions;
using Sif.Framework.Model.Infrastructure;
using Sif.Framework.Model.Query;
using Sif.Framework.Model.Responses;
using Sif.Framework.Service;
using Sif.Framework.Service.Authentication;
using Sif.Framework.Service.Infrastructure;
using Sif.Framework.Service.Mapper;
using Sif.Framework.Service.Providers;
using Sif.Framework.Utils;
using Sif.Framework.WebApi.ModelBinders;
using Sif.Specification.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace Sif.Framework.Providers
{

    /// <summary>
    /// This class defines a Provider of SIF data model objects.
    /// </summary>
    /// <typeparam name="TSingle">Type that defines a single object entity.</typeparam>
    /// <typeparam name="TMultiple">Type that defines a multiple objects entity.</typeparam>
    public abstract class Provider<TSingle, TMultiple> : ApiController, IProvider<TSingle, TMultiple, string> where TSingle : ISifRefId<string>
    {

        /// <summary>
        /// Service used for request authentication.
        /// </summary>
        protected IAuthenticationService authService;

        /// <summary>
        /// Object service associated with this Provider.
        /// </summary>
        protected IObjectService<TSingle, TMultiple, string> service;

        /// <summary>
        /// Default constructor that is only available to derived instances of
        /// this class.
        /// </summary>
        protected Provider()
        {

            if (EnvironmentType.DIRECT.Equals(SettingsManager.ProviderSettings.EnvironmentType))
            {
                authService = new DirectAuthenticationService(new ApplicationRegisterService(), new EnvironmentService());
            }
            else if (EnvironmentType.BROKERED.Equals(SettingsManager.ProviderSettings.EnvironmentType))
            {
                authService = new BrokeredAuthenticationService(new ApplicationRegisterService(), new EnvironmentService());
            }

        }

        /// <summary>
        /// Create an instance based on the specified service.
        /// </summary>
        /// <param name="service">Service used for managing the object type.</param>
        public Provider(IProviderService<TSingle, TMultiple> service)
            : base()
        {
            this.service = service;
        }

        /// <summary>
        /// <see cref="IProvider{TTSingle,TMultiple,TPrimaryKey}.Post(TTSingle, string[], string[])">Post</see>
        /// </summary>
        public virtual IHttpActionResult Post(TSingle obj, [MatrixParameter] string[] zoneId = null, [MatrixParameter] string[] contextId = null)
        {

            if (!authService.VerifyAuthenticationHeader(Request.Headers))
            {
                return Unauthorized();
            }

            // Check ACLs and return StatusCode(HttpStatusCode.Forbidden) if appropriate.

            if ((zoneId != null && zoneId.Length != 1) || (contextId != null && contextId.Length != 1))
            {
                return BadRequest("Request failed for object " + typeof(TSingle).Name + " as Zone and/or Context are invalid.");
            }

            IHttpActionResult result;

            try
            {
                bool hasAdvisoryId = !string.IsNullOrWhiteSpace(obj.RefId);
                bool? mustUseAdvisory = HttpUtils.GetMustUseAdvisory(Request.Headers);

                if (hasAdvisoryId)
                {

                    if (mustUseAdvisory.HasValue && mustUseAdvisory.Value == true)
                    {
                        TSingle createdObject = service.Create(obj, mustUseAdvisory, zoneId: (zoneId == null ? null : zoneId[0]), contextId: (contextId == null ? null : contextId[0]));
                        string uri = Url.Link("DefaultApi", new { controller = typeof(TSingle).Name, id = createdObject.RefId });
                        result = Created(uri, createdObject);
                    }
                    else
                    {
                        result = BadRequest("Request failed for object " + typeof(TSingle).Name + " as object ID provided (" + obj.RefId + "), but mustUseAdvisory is not specified or is false.");
                    }

                }
                else
                {

                    if (mustUseAdvisory.HasValue && mustUseAdvisory.Value == true)
                    {
                        result = BadRequest("Request failed for object " + typeof(TSingle).Name + " as object ID is not provided, but mustUseAdvisory is true.");
                    }
                    else
                    {
                        TSingle createdObject = service.Create(obj, zoneId: (zoneId == null ? null : zoneId[0]), contextId: (contextId == null ? null : contextId[0]));
                        string uri = Url.Link("DefaultApi", new { controller = typeof(TSingle).Name, id = createdObject.RefId });
                        result = Created(uri, createdObject);
                    }

                }

            }
            catch (AlreadyExistsException)
            {
                result = Conflict();
            }
            catch (ArgumentException e)
            {
                result = BadRequest("Object to create of type " + typeof(TSingle).Name + " is invalid.\n " + e.Message);
            }
            catch (CreateException e)
            {
                result = BadRequest("Request failed for object " + typeof(TSingle).Name + ".\n " + e.Message);
            }
            catch (RejectedException e)
            {
                result = this.NotFound("Create request rejected for object " + typeof(TSingle).Name + " with ID of " + obj.RefId + ".\n" + e.Message);
            }
            catch (QueryException e)
            {
                result = BadRequest("Request failed for object " + typeof(TSingle).Name + ".\n " + e.Message);
            }
            catch (Exception e)
            {
                result = InternalServerError(e);
            }

            return result;
        }

        /// <summary>
        /// <see cref="IProvider{TTSingle,TMultiple,TPrimaryKey}.Post(TMultiple, string[], string[])">Post</see>
        /// </summary>
        public virtual IHttpActionResult Post(TMultiple obj, [MatrixParameter] string[] zoneId = null, [MatrixParameter] string[] contextId = null)
        {

            if (!authService.VerifyAuthenticationHeader(Request.Headers))
            {
                return Unauthorized();
            }

            // Check ACLs and return StatusCode(HttpStatusCode.Forbidden) if appropriate.

            if ((zoneId != null && zoneId.Length != 1) || (contextId != null && contextId.Length != 1))
            {
                return BadRequest("Request failed for object " + typeof(TSingle).Name + " as Zone and/or Context are invalid.");
            }

            bool? mustUseAdvisory = HttpUtils.GetMustUseAdvisory(Request.Headers);
            MultipleCreateResponse multipleCreateResponse =
                ((IProviderService<TSingle, TMultiple>)service).Create(obj, mustUseAdvisory, zoneId: (zoneId == null ? null : zoneId[0]), contextId: (contextId == null ? null : contextId[0]));
            createResponseType createResponse = MapperFactory.CreateInstance<MultipleCreateResponse, createResponseType>(multipleCreateResponse);
            IHttpActionResult result = Ok(createResponse);

            return result;
        }

        /// <summary>
        /// <see cref="IProvider{TTSingle,TMultiple,TPrimaryKey}.Get(TPrimaryKey, string[], string[])">Get</see>
        /// </summary>
        public virtual IHttpActionResult Get([FromUri(Name = "id")] string refId, [MatrixParameter] string[] zoneId = null, [MatrixParameter] string[] contextId = null)
        {

            if (!authService.VerifyAuthenticationHeader(Request.Headers))
            {
                return Unauthorized();
            }

            // Check ACLs and return StatusCode(HttpStatusCode.Forbidden) if appropriate.

            if (HttpUtils.HasPagingHeaders(Request.Headers))
            {
                return StatusCode(HttpStatusCode.MethodNotAllowed);
            }

            if ((zoneId != null && zoneId.Length != 1) || (contextId != null && contextId.Length != 1))
            {
                return BadRequest("Request failed for object " + typeof(TSingle).Name + " as Zone and/or Context are invalid.");
            }

            IHttpActionResult result;

            try
            {
                TSingle obj = service.Retrieve(refId, zoneId: (zoneId == null ? null : zoneId[0]), contextId: (contextId == null ? null : contextId[0]));

                if (obj == null)
                {
                    result = NotFound();
                }
                else
                {
                    result = Ok(obj);
                }

            }
            catch (ArgumentException e)
            {
                result = BadRequest("Invalid argument: id=" + refId + ".\n" + e.Message);
            }
            catch (QueryException e)
            {
                result = BadRequest("Request failed for object " + typeof(TSingle).Name + " with ID of " + refId + ".\n " + e.Message);
            }
            catch (Exception e)
            {
                result = InternalServerError(e);
            }

            return result;
        }

        /// <summary>
        /// Retrieve all objects.
        /// </summary>
        /// <param name="navigationPage">Current paging index.</param>
        /// <param name="navigationPageSize">Page size.</param>
        /// <param name="requestedZone">Zone associated with the request.</param>
        /// <param name="requestedContext">Zone context.</param>
        /// <exception cref="ArgumentException">One or more parameters of the Provider service call are invalid.</exception>
        /// <exception cref="ContentTooLargeException">Too many objects to return.</exception>
        /// <exception cref="QueryException">Error retrieving objects.</exception>
        /// <exception cref="Exception">Catch all for exceptions thrown by the implementation of the Provider service interface.</exception>
        /// <returns>All objects.</returns>
        private IHttpActionResult GetAll(uint? navigationPage, uint? navigationPageSize, string requestedZone, string requestedContext)
        {

            if (HttpUtils.HasMethodOverrideHeader(Request.Headers))
            {
                return BadRequest("GET (Query by Example) request failed due to missing payload.");
            }

            IHttpActionResult result;
            TMultiple objs = service.Retrieve(navigationPage, navigationPageSize, requestedZone, requestedContext);

            if (objs == null)
            {
                result = StatusCode(HttpStatusCode.NoContent);
            }
            else
            {
                result = Ok(objs);
            }

            return result;
        }

        /// <summary>
        /// Retrieve objects based on the Changes Since marker.
        /// </summary>
        /// <param name="changesSinceMarker">Changes Since marker.</param>
        /// <param name="navigationPage">Current paging index.</param>
        /// <param name="navigationPageSize">Page size.</param>
        /// <param name="requestedZone">Zone associated with the request.</param>
        /// <param name="requestedContext">Zone context.</param>
        /// <exception cref="ArgumentException">One or more parameters of the Provider service call are invalid.</exception>
        /// <exception cref="ContentTooLargeException">Too many objects to return.</exception>
        /// <exception cref="QueryException">Error retrieving objects.</exception>
        /// <exception cref="Exception">Catch all for exceptions thrown by the implementation of the Provider service interface.</exception>
        /// <returns>Objects associated with the Changes Since marker.</returns>
        private IHttpActionResult GetChangesSince(string changesSinceMarker, uint? navigationPage, uint? navigationPageSize, string requestedZone, string requestedContext)
        {

            if (HttpUtils.HasMethodOverrideHeader(Request.Headers))
            {
                return BadRequest("The Changes Since marker is not applicable for a GET (Query by Example) request.");
            }

            bool changesSinceRequested = !string.IsNullOrWhiteSpace(changesSinceMarker);
            IChangesSinceService<TMultiple> changesSinceService = service as IChangesSinceService<TMultiple>;
            bool changesSinceSupported = (changesSinceService != null);

            if (changesSinceRequested && !changesSinceSupported)
            {
                return BadRequest("The Changes Since request is not supported.");
            }

            IHttpActionResult result;
            TMultiple objs = changesSinceService.RetrieveChangesSince(changesSinceMarker, navigationPage, navigationPageSize, requestedZone, requestedContext);

            if (objs == null)
            {
                result = StatusCode(HttpStatusCode.NoContent);
            }
            else
            {
                result = Ok(objs);
            }

            bool pagedRequest = navigationPage.HasValue && navigationPageSize.HasValue;
            bool firstPage = (navigationPage.HasValue && navigationPage.Value == 1);

            // Changes Since marker is only returned for non-paged requests or the first page of a paged request.
            if (!pagedRequest || firstPage)
            {

                try
                {
                    result = result.AddHeader("changesSinceMarker", changesSinceService.NextChangesSinceMarker ?? string.Empty);
                }
                catch (Exception)
                {
                    throw new QueryException("Implementaton to retrieve the next Changes Since marker returned an error.");
                }

            }

            return result;
        }

        /// <summary>
        /// Retrieve objects using Query by Example.
        /// </summary>
        /// <param name="obj">Example object.</param>
        /// <param name="navigationPage">Current paging index.</param>
        /// <param name="navigationPageSize">Page size.</param>
        /// <param name="requestedZone">Zone associated with the request.</param>
        /// <param name="requestedContext">Zone context.</param>
        /// <exception cref="ArgumentException">One or more parameters of the Provider service call are invalid.</exception>
        /// <exception cref="ContentTooLargeException">Too many objects to return.</exception>
        /// <exception cref="QueryException">Error retrieving objects.</exception>
        /// <exception cref="Exception">Catch all for exceptions thrown by the implementation of the Provider service interface.</exception>
        /// <returns>Objects which match the Query by Example.</returns>
        private IHttpActionResult GetQueryByExample(TSingle obj, uint? navigationPage, uint? navigationPageSize, string requestedZone, string requestedContext)
        {

            if (!HttpUtils.HasMethodOverrideHeader(Request.Headers))
            {
                return BadRequest("GET (Query by Example) request failed due to a missing method override header.");
            }

            IHttpActionResult result;
            TMultiple objs = service.Retrieve(obj, navigationPage, navigationPageSize, requestedZone, requestedContext);

            if (objs == null)
            {
                result = StatusCode(HttpStatusCode.NoContent);
            }
            else
            {
                result = Ok(objs);
            }

            return result;
        }

        /// <summary>
        /// <see cref="IProvider{TTSingle,TMultiple,TPrimaryKey}.Get(TTSingle, string, string[], string[])">Get</see>
        /// </summary>
        public virtual IHttpActionResult Get(TSingle obj, string changesSinceMarker = null, [MatrixParameter] string[] zoneId = null, [MatrixParameter] string[] contextId = null)
        {

            if (!authService.VerifyAuthenticationHeader(Request.Headers))
            {
                return Unauthorized();
            }

            // Check ACLs and return StatusCode(HttpStatusCode.Forbidden) if appropriate.

            string errorMessage;

            if (!HttpUtils.ValidatePagingParameters(Request.Headers, out errorMessage))
            {
                return BadRequest(errorMessage);
            }

            if ((zoneId != null && zoneId.Length != 1) || (contextId != null && contextId.Length != 1))
            {
                return BadRequest("Request failed for object " + typeof(TSingle).Name + " as Zone and/or Context are invalid.");
            }

            IHttpActionResult result;

            try
            {
                uint? navigationPage = HttpUtils.GetNavigationPage(Request.Headers);
                uint? navigationPageSize = HttpUtils.GetNavigationPageSize(Request.Headers);
                string requestedZone = (zoneId == null ? null : zoneId[0]);
                string requestedContext = (contextId == null ? null : contextId[0]);

                if (obj == null)
                {

                    if (changesSinceMarker == null)
                    {
                        result = GetAll(navigationPage, navigationPageSize, requestedZone, requestedContext);
                    }
                    else
                    {
                        result = GetChangesSince(changesSinceMarker, navigationPage, navigationPageSize, requestedZone, requestedContext);
                    }

                }
                else
                {
                    result = GetQueryByExample(obj, navigationPage, navigationPageSize, requestedZone, requestedContext);
                }

            }
            catch (ArgumentException e)
            {
                result = BadRequest("One or more parameters of the GET request are invalid.\n" + e.Message);
            }
            catch (QueryException e)
            {
                result = BadRequest("GET request failed for object " + typeof(TSingle).Name + ".\n " + e.Message);
            }
            catch (ContentTooLargeException)
            {
                result = StatusCode(HttpStatusCode.RequestEntityTooLarge);
            }
            catch (Exception e)
            {
                result = InternalServerError(e);
            }

            return result;
        }

        /// <summary>
        /// <see cref="IProvider{TTSingle,TMultiple,TPrimaryKey}.Get(string, string, string, string, string, string, string[], string[])">Get</see>
        /// </summary>
        public virtual IHttpActionResult Get(string object1,
            [FromUri(Name = "id1")] string refId1,
            string object2 = null,
            [FromUri(Name = "id2")] string refId2 = null,
            string object3 = null,
            [FromUri(Name = "id3")] string refId3 = null,
            [MatrixParameter] string[] zoneId = null,
            [MatrixParameter] string[] contextId = null)
        {

            if (!authService.VerifyAuthenticationHeader(Request.Headers))
            {
                return Unauthorized();
            }

            // Check ACLs and return StatusCode(HttpStatusCode.Forbidden) if appropriate.

            // Check ACLs and return StatusCode(HttpStatusCode.BadRequest) if parameters do not match a recognised Service Path.

            string errorMessage;

            if (!HttpUtils.ValidatePagingParameters(Request.Headers, out errorMessage))
            {
                return BadRequest(errorMessage);
            }

            if ((zoneId != null && zoneId.Length != 1) || (contextId != null && contextId.Length != 1))
            {
                return BadRequest("Request failed for object " + typeof(TSingle).Name + " as Zone and/or Context are invalid.");
            }

            IHttpActionResult result;

            try
            {
                IList<EqualCondition> conditions = new List<EqualCondition>() { new EqualCondition() { Left = object1, Right = refId1 } };

                if (!string.IsNullOrWhiteSpace(object2))
                {
                    conditions.Add(new EqualCondition() { Left = object2, Right = refId2 });

                    if (!string.IsNullOrWhiteSpace(object3))
                    {
                        conditions.Add(new EqualCondition() { Left = object3, Right = refId3 });
                    }

                }

                TMultiple objs = service.Retrieve(conditions, zoneId: (zoneId == null ? null : zoneId[0]), contextId: (contextId == null ? null : contextId[0]));

                if (objs == null)
                {
                    result = StatusCode(HttpStatusCode.NoContent);
                }
                else
                {
                    result = Ok(objs);
                }

            }
            catch (ArgumentException e)
            {
                result = BadRequest("One or more conditions are invalid.\n" + e.Message);
            }
            catch (QueryException e)
            {
                result = BadRequest("Service Path GET request failed for object " + typeof(TSingle).Name + ".\n " + e.Message);
            }
            catch (ContentTooLargeException)
            {
                result = StatusCode(HttpStatusCode.RequestEntityTooLarge);
            }
            catch (Exception e)
            {
                result = InternalServerError(e);
            }

            return result;
        }

        /// <summary>
        /// <see cref="IProvider{TTSingle,TMultiple,TPrimaryKey}.Put(TPrimaryKey, TTSingle, string[], string[])">Put</see>
        /// </summary>
        public virtual IHttpActionResult Put([FromUri(Name = "id")] string refId, TSingle obj, [MatrixParameter] string[] zoneId = null, [MatrixParameter] string[] contextId = null)
        {

            if (!authService.VerifyAuthenticationHeader(Request.Headers))
            {
                return Unauthorized();
            }

            // Check ACLs and return StatusCode(HttpStatusCode.Forbidden) if appropriate.

            if (string.IsNullOrWhiteSpace(refId) || obj == null || !refId.Equals(obj.RefId))
            {
                return BadRequest("The refId in the update request does not match the SIF identifier of the object provided.");
            }

            if ((zoneId != null && zoneId.Length != 1) || (contextId != null && contextId.Length != 1))
            {
                return BadRequest("Request failed for object " + typeof(TSingle).Name + " as Zone and/or Context are invalid.");
            }

            IHttpActionResult result;

            try
            {
                service.Update(obj, zoneId: (zoneId == null ? null : zoneId[0]), contextId: (contextId == null ? null : contextId[0]));
                result = StatusCode(HttpStatusCode.NoContent);
            }
            catch (ArgumentException e)
            {
                result = BadRequest("Object to update of type " + typeof(TSingle).Name + " is invalid.\n " + e.Message);
            }
            catch (NotFoundException e)
            {
                result = this.NotFound("Object " + typeof(TSingle).Name + " with ID of " + refId + " not found.\n" + e.Message);
            }
            catch (UpdateException e)
            {
                result = BadRequest("Request failed for object " + typeof(TSingle).Name + " with ID of " + refId + ".\n " + e.Message);
            }
            catch (Exception e)
            {
                result = InternalServerError(e);
            }

            return result;
        }

        /// <summary>
        /// <see cref="IProvider{TTSingle,TMultiple,TPrimaryKey}.Put(TMultiple, string[], string[])">Put</see>
        /// </summary>
        public virtual IHttpActionResult Put(TMultiple obj, [MatrixParameter] string[] zoneId = null, [MatrixParameter] string[] contextId = null)
        {

            if (!authService.VerifyAuthenticationHeader(Request.Headers))
            {
                return Unauthorized();
            }

            // Check ACLs and return StatusCode(HttpStatusCode.Forbidden) if appropriate.

            if ((zoneId != null && zoneId.Length != 1) || (contextId != null && contextId.Length != 1))
            {
                return BadRequest("Request failed for object " + typeof(TSingle).Name + " as Zone and/or Context are invalid.");
            }

            MultipleUpdateResponse multipleUpdateResponse = 
                ((IProviderService<TSingle, TMultiple>)service).Update(obj, zoneId: (zoneId == null ? null : zoneId[0]), contextId: (contextId == null ? null : contextId[0]));
            updateResponseType updateResponse = MapperFactory.CreateInstance<MultipleUpdateResponse, updateResponseType>(multipleUpdateResponse);
            IHttpActionResult result = Ok(updateResponse);

            return result;
        }

        /// <summary>
        /// <see cref="IProvider{TTSingle,TMultiple,TPrimaryKey}.Delete(TPrimaryKey, string[], string[])">Delete</see>
        /// </summary>
        public virtual IHttpActionResult Delete([FromUri(Name = "id")] string refId, [MatrixParameter] string[] zoneId = null, [MatrixParameter] string[] contextId = null)
        {

            if (!authService.VerifyAuthenticationHeader(Request.Headers))
            {
                return Unauthorized();
            }

            // Check ACLs and return StatusCode(HttpStatusCode.Forbidden) if appropriate.

            if ((zoneId != null && zoneId.Length != 1) || (contextId != null && contextId.Length != 1))
            {
                return BadRequest("Request failed for object " + typeof(TSingle).Name + " as Zone and/or Context are invalid.");
            }

            IHttpActionResult result;

            try
            {
                service.Delete(refId, zoneId: (zoneId == null ? null : zoneId[0]), contextId: (contextId == null ? null : contextId[0]));
                result = StatusCode(HttpStatusCode.NoContent);
            }
            catch (ArgumentException e)
            {
                result = BadRequest("Invalid argument: id=" + refId + ".\n" + e.Message);
            }
            catch (DeleteException e)
            {
                result = BadRequest("Request failed for object " + typeof(TSingle).Name + " with ID of " + refId + ".\n " + e.Message);
            }
            catch (NotFoundException e)
            {
                result = this.NotFound("Object " + typeof(TSingle).Name + " with ID of " + refId + " not found.\n" + e.Message);
            }
            catch (Exception e)
            {
                result = InternalServerError(e);
            }

            return result;
        }

        /// <summary>
        /// <see cref="IProvider{TTSingle,TMultiple,TPrimaryKey}.Delete(deleteRequestType, string[], string[])">Delete</see>
        /// </summary>
        public virtual IHttpActionResult Delete(deleteRequestType deleteRequest, [MatrixParameter] string[] zoneId = null, [MatrixParameter] string[] contextId = null)
        {

            if (!authService.VerifyAuthenticationHeader(Request.Headers))
            {
                return Unauthorized();
            }

            // Check ACLs and return StatusCode(HttpStatusCode.Forbidden) if appropriate.

            if ((zoneId != null && zoneId.Length != 1) || (contextId != null && contextId.Length != 1))
            {
                return BadRequest("Request failed for object " + typeof(TSingle).Name + " as Zone and/or Context are invalid.");
            }

            IHttpActionResult result;
            ICollection<deleteStatus> deleteStatuses = new List<deleteStatus>();

            try
            {

                foreach (deleteIdType deleteId in deleteRequest.deletes)
                {
                    deleteStatus status = new deleteStatus();
                    status.id = deleteId.id;

                    try
                    {
                        service.Delete(deleteId.id, zoneId: (zoneId == null ? null : zoneId[0]), contextId: (contextId == null ? null : contextId[0]));
                        status.statusCode = ((int)HttpStatusCode.NoContent).ToString();
                    }
                    catch (ArgumentException e)
                    {
                        status.error = ProviderUtils.CreateError(HttpStatusCode.BadRequest, typeof(TSingle).Name, "Invalid argument: id=" + deleteId.id + ".\n" + e.Message);
                        status.statusCode = ((int)HttpStatusCode.BadRequest).ToString();
                    }
                    catch (DeleteException e)
                    {
                        status.error = ProviderUtils.CreateError(HttpStatusCode.BadRequest, typeof(TSingle).Name, "Request failed for object " + typeof(TSingle).Name + " with ID of " + deleteId.id + ".\n " + e.Message);
                        status.statusCode = ((int)HttpStatusCode.BadRequest).ToString();
                    }
                    catch (NotFoundException e)
                    {
                        status.error = ProviderUtils.CreateError(HttpStatusCode.NotFound, typeof(TSingle).Name, "Object " + typeof(TSingle).Name + " with ID of " + deleteId.id + " not found.\n" + e.Message);
                        status.statusCode = ((int)HttpStatusCode.NotFound).ToString();
                    }
                    catch (Exception e)
                    {
                        status.error = ProviderUtils.CreateError(HttpStatusCode.InternalServerError, typeof(TSingle).Name, "Request failed for object " + typeof(TSingle).Name + " with ID of " + deleteId.id + ".\n " + e.Message);
                        status.statusCode = ((int)HttpStatusCode.InternalServerError).ToString();
                    }

                    deleteStatuses.Add(status);
                }

            }
            catch (Exception)
            {
                // Need to ignore exceptions otherwise it would not be possible to return status records of processed objects.
            }

            deleteResponseType deleteResponse = new deleteResponseType { deletes = deleteStatuses.ToArray() };
            result = Ok(deleteResponse);

            return result;
        }

        /// <summary>
        /// <see cref="IProvider{TTSingle,TMultiple,TPrimaryKey}.Head(string[], string[])">Head</see>
        /// </summary>
        [HttpHead]
        public virtual IHttpActionResult Head([MatrixParameter] string[] zoneId = null, [MatrixParameter] string[] contextId = null)
        {

            if (!authService.VerifyAuthenticationHeader(Request.Headers))
            {
                return Unauthorized();
            }

            // Check ACLs and return StatusCode(HttpStatusCode.Forbidden) if appropriate.

            string errorMessage;

            if (!HttpUtils.ValidatePagingParameters(Request.Headers, out errorMessage))
            {
                return BadRequest(errorMessage);
            }

            if ((zoneId != null && zoneId.Length != 1) || (contextId != null && contextId.Length != 1))
            {
                return BadRequest("Request failed for object " + typeof(TSingle).Name + " as Zone and/or Context are invalid.");
            }

            IHttpActionResult result;

            try
            {
                uint? navigationPage = HttpUtils.GetNavigationPage(Request.Headers);
                uint? navigationPageSize = HttpUtils.GetNavigationPageSize(Request.Headers);
                string requestedZone = (zoneId == null ? null : zoneId[0]);
                string requestedContext = (contextId == null ? null : contextId[0]);
                result = GetAll(navigationPage, navigationPageSize, requestedZone, requestedContext).ClearContent();
                ISupportsChangesSince supportsChangesSince = service as ISupportsChangesSince;

                if (supportsChangesSince != null)
                {
                    result = result.AddHeader("changesSinceMarker", supportsChangesSince.ChangesSinceMarker ?? string.Empty);
                }

            }
            catch (ArgumentException e)
            {
                result = BadRequest("One or more parameters of the GET request (associated with the HEAD request) are invalid.\n" + e.Message);
            }
            catch (QueryException e)
            {
                result = BadRequest("HEAD request failed for object " + typeof(TSingle).Name + ".\n " + e.Message);
            }
            catch (ContentTooLargeException)
            {
                result = StatusCode(HttpStatusCode.RequestEntityTooLarge);
            }
            catch (Exception e)
            {
                result = InternalServerError(e);
            }

            return result;
        }

    }

}
