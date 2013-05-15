﻿using System.Web.Mvc;
using SalesHub.Client.Builders;
using SalesHub.Client.Converters;
using SalesHub.Client.Processors;
using SalesHub.Client.ViewModels.Client;
using SalesHub.Core.Models;
using SalesHub.Core.Repositories;
using SalesHub.Data.Repositories;

namespace SalesHub.Client.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IPaymentTermProcessor _paymentTermProcessor;
        private readonly ICustomerPathBuilder _customerPathBuilder;
        private readonly IOrderViewModelSelectListBuilder _orderViewModelSelectListBuilder;
        private readonly IOrderViewDataBuilder _orderViewDataBuilder;
        private readonly IConverter<OrderViewModel, Order> _orderViewModelConverter;
        private readonly ISuggestedValueRepository _suggestedValueRepository;

        public OrderController(IOrderRepository orderRepository,
            ICustomerRepository customerRepository,
            IPaymentTermProcessor paymentTermProcessor,
            ICustomerPathBuilder customerPathBuilder,
            IOrderViewModelSelectListBuilder orderViewModelSelectListBuilder,
            IOrderViewDataBuilder orderViewDataBuilder,
            IConverter<OrderViewModel, Order> orderViewModelConverter,
            ISuggestedValueRepository suggestedValueRepository)
        {
            _orderRepository = orderRepository;
            _customerRepository = customerRepository;
            _paymentTermProcessor = paymentTermProcessor;
            _customerPathBuilder = customerPathBuilder;
            _orderViewModelSelectListBuilder = orderViewModelSelectListBuilder;
            _orderViewDataBuilder = orderViewDataBuilder;
            _orderViewModelConverter = orderViewModelConverter;
            _suggestedValueRepository = suggestedValueRepository;
        }

        public ActionResult Edit(int id)
        {
            Order order = _orderRepository.GetOrderWithPaymentTermsById(id);

            _orderViewDataBuilder.BuildViewData(ViewData, order);

            if(order.PaymentTerm1 == null)
                order.PaymentTerm1 = new PaymentTerm();
            if(order.PaymentTerm2 == null)
                order.PaymentTerm2 = new PaymentTerm();

            OrderViewModel viewModel = _orderViewModelConverter.Convert(order);
            SetViewModelFields(viewModel, order.CustomerId);
            return View(viewModel);
        }

        [HttpPost]
        public ActionResult Edit(OrderViewModel orderEditViewModel)
        {
            Order order = _orderRepository.GetOrderById(orderEditViewModel.OrderId);

            _orderViewDataBuilder.BuildViewData(ViewData, order);

            if (orderEditViewModel.PaymentTerm1.PaymentTerm.SplitPercentage + orderEditViewModel.PaymentTerm2.PaymentTerm.SplitPercentage != 1M)
            {
                ModelState.AddModelError("", "Split Percentages Must Total 100%");
            }
            if (!ModelState.IsValid)
            {
                SetViewModelFields(orderEditViewModel, order.CustomerId);
                return View(orderEditViewModel);
            }

            CopyOrderViewModelToOrder(orderEditViewModel, order);

            orderEditViewModel.PaymentTerm1.PaymentTerm.PaymentTermId = order.PaymentTerm1 != null ? order.PaymentTerm1.PaymentTermId : 0;
            orderEditViewModel.PaymentTerm2.PaymentTerm.PaymentTermId = order.PaymentTerm2 != null ? order.PaymentTerm2.PaymentTermId : 0;
            order.PaymentTerm1 = _paymentTermProcessor.ProcessPaymentTerm(orderEditViewModel.PaymentTerm1.PaymentTerm);
            order.PaymentTerm2 = _paymentTermProcessor.ProcessPaymentTerm(orderEditViewModel.PaymentTerm2.PaymentTerm);

            _orderRepository.SaveChanges();

            return RedirectToRoute("Default", new { controller = "Order", action = "Edit", id = orderEditViewModel.OrderId });
        }

        public ActionResult New(int id)
        {
            var orderViewModel = new OrderViewModel
            {
                PaymentTerm1 = new OrderPaymentTermViewModel
                {
                    PaymentTerm = new PaymentTerm()
                },
                PaymentTerm2 = new OrderPaymentTermViewModel
                {
                    PaymentTerm = new PaymentTerm()
                },
                IsNew = true
            };
            SetViewModelFields(orderViewModel, id);
            return View(orderViewModel);
        }

        [HttpPost]
        public ActionResult New(int id, OrderViewModel orderViewModel)
        {
            Customer customer = _customerRepository.GetCustomerById(id);

            if (orderViewModel.PaymentTerm1.PaymentTerm.SplitPercentage + orderViewModel.PaymentTerm2.PaymentTerm.SplitPercentage != 1M)
            {
                ModelState.AddModelError("", "Split Percentages Must Total 100%");
            }
            if (!ModelState.IsValid)
            {
                SetViewModelFields(orderViewModel, id);
                return View(orderViewModel);
            }

            var order = new Order();

            CopyOrderViewModelToOrder(orderViewModel, order);
            order.CustomerId = customer.CustomerId;

            _orderRepository.Add(order);
            _orderRepository.SaveChanges();

            return RedirectToRoute("Default", new { action = "Edit", controller = "Order", id = order.OrderId });
        }

        [HttpPost]
        public ActionResult Delete(int id)
        {
            var order = _orderRepository.GetOrderById(id);
            int customerId = order.CustomerId;

            _orderRepository.Delete(order);
            _orderRepository.SaveChanges();

            return RedirectToRoute("default", new {controller = "Home", action = "Index", customerId});
        }

        private void CopyOrderViewModelToOrder(OrderViewModel orderViewModel, Order order)
        {
            order.OrderId = orderViewModel.OrderId;
            order.OrderNumber = orderViewModel.OrderNumber;
            order.ContractWeight = orderViewModel.ContractWeight;
            order.ContractAmount = orderViewModel.ContractAmount;
            order.OrderDate = orderViewModel.OrderDate;
            order.IsActive = orderViewModel.IsActive;
            order.ContractCurrencyTypeId = order.ContractCurrencyTypeId;
            order.PaymentTermsOverride = orderViewModel.PaymentTermsOverride;
            order.IntentComments = orderViewModel.IntentComments;
            order.InvoiceComments = orderViewModel.InvoiceComments;
            order.FooterComments = orderViewModel.FooterComments;
            order.HeaderComments = orderViewModel.HeaderComments;
        }

        private void SetViewModelFields(OrderViewModel viewModel, int customerId)
        {
            Customer customer = _customerRepository.GetCustomerById(customerId);
            viewModel.CustomerId = customerId;
            viewModel.CustomerPath = _customerPathBuilder.BuildCustomerPath(customer.SellingCompany, customer);
            viewModel.SuggestedValues = _suggestedValueRepository.GetAllSuggestedValues();
            viewModel.Customer = _customerRepository.GetCustomerById(viewModel.CustomerId);
            _orderViewModelSelectListBuilder.BuildSelectListsForOrderViewModel(viewModel);
        }
    }
}
