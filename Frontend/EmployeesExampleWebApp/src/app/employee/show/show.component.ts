import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AddEditEmployeeComponent } from '../add-edit/add-edit.component';
import { Employee } from '../../models/employee';
import { WebAPIService } from '../../web-api.service';
import { NgbModal, NgbAlert } from '@ng-bootstrap/ng-bootstrap';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';

@Component({
  selector: 'app-show-employee',
  imports: [CommonModule, NgbAlert, AddEditEmployeeComponent],
  templateUrl: './show.component.html',
  styleUrls: ['./show.component.css']
})
export class ShowEmployeeComponent implements OnInit {

  constructor(public service: WebAPIService, private modalService: NgbModal) { }

  private _success = new Subject<string>();
  employeeList: Employee[] = [];

  ModalTitle = 'Modal Title';
  ActivateAddEditEmployeeComponent = false;
  employee: Employee = { EmployeeId: 0, EmployeeName: '', Department: '', DateOfJoining: '', PhotoFileName: 'anonymous.png' };
  closeResult = '';
  successMessage = '';

  @ViewChild('selfClosingAlert') alert: NgbAlert | undefined;

  ngOnInit(): void {
    this.refreshEmployeeList();
    this._success.subscribe((message) => this.successMessage = message);
    this._success.pipe(debounceTime(5000)).subscribe(() => {
      if (this.alert) {
        this.alert.close();
      }
    }
    );
  }

  onAdd(content: any) {
    this.employee = { EmployeeId: 0, EmployeeName: '', Department: '', DateOfJoining: '', PhotoFileName: 'anonymous.png' };
    this.ModalTitle = 'Add Department';
    this.ActivateAddEditEmployeeComponent = true;
    this.modalService.open(content, { ariaLabelledBy: 'modal-add-employee', size: 'xl', centered: true }).result.then((result) => {
      this.closeResult = `Closed with: ${result}`;
    }
    );
  }

  onClose(modal: any) {
    this.closeModal(modal, 'Closed');
  }

  public closeModal(modal: any, reason: string) {
    this.ActivateAddEditEmployeeComponent = false;
    this.refreshEmployeeList();
    modal.dismiss(reason);
  }

  public showSuccess(message: string) {
    this._success.next(message);
  }

  refreshEmployeeList() {
    this.service.getEmployees().subscribe(
      result => {
        this.employeeList = result;
      }
    );
  }

  onEdit(employee: Employee, content: any) {
    this.employee = employee;
    this.ModalTitle = 'Edit Employee';
    this.ActivateAddEditEmployeeComponent = true;
    this.modalService.open(content, { ariaLabelledBy: 'modal-edit-employee', size: 'xl', centered: true }).result.then((result) => {
      this.closeResult = `Closed with: ${result}`;
    }
    );
  }

  onDelete(employee: Employee) {
    if (confirm('Are you sure to remove this employee?')) {
      this.service.deleteEmployee(employee.EmployeeId).subscribe(
        result => {
          this.showSuccess(result.Message);
          this.refreshEmployeeList();
        }
      );
    }
  }

}
