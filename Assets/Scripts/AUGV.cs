/**
* AUGV.cs
* This script is responsible for controlling the AUGV.
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class AUGV : MonoBehaviour {
    public float moveSpeed = 2f, rotationSpeed = 4f, waitAtWaypoint = 2f;

    private string agentId;
    public enum AgentState { Idle, WaitingForStep, WaitingAtTarget };
    public AgentState State { get; set; } = AgentState.Idle;

    private List<Node> currentPath = new List<Node>();
    private int currentIndex = 0;
    
    private Coroutine moveCoroutine;

    private bool pathChanged = false;

    void Start() {
        agentId = gameObject.name;
    }

    public void Advance() {

        if (pathChanged) {
            // if (moveCoroutine != null) StopCoroutine(moveCoroutine);
            currentPath = PathSupervisor.Instance.activePaths[agentId];
            currentIndex = 0;
            // if (currentPath == null || currentIndex >= currentPath.Count) return;
            pathChanged = false;
            // State = AgentState.WaitingForStep;
            // PathSupervisor.Instance.ReportAgentActive(agentId);
        }

        if (currentPath == null || currentIndex >= currentPath.Count) return;

        var targetNode = currentPath[currentIndex];
        var targetPosition = new Vector3(targetNode.worldPosition.x, transform.position.y, targetNode.worldPosition.z);
        
        // PURPOSE: performance improvement.
        // TODO: this is a temporary fix to avoid multiple move coroutine running at the same time.
        // TODO: we need to find a better way to handle this.
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);

        moveCoroutine = StartCoroutine(_Move(targetPosition));
    }

    public void PathChanged() {
        pathChanged = true;
    }

    /// MOVE COUROUTINE.

    public void AssignPath(List<Node> path) {
        currentPath = PathSupervisor.Instance.activePaths[agentId];
        currentIndex = 0;
        if (path == null ||
            path.Count == 0) {
                State = AgentState.Idle;
                PathSupervisor.Instance.ReportAgentActive(agentId);
                return;
            }
        
        State = AgentState.WaitingForStep;
        PathSupervisor.Instance.ReportAgentActive(agentId);
    }

    public bool IsReadyToStep() => State == AgentState.WaitingForStep;

    private IEnumerator _Move(Vector3 target) {
        while (Vector3.Distance(transform.position, target) > 0.05f) {
            var direction = (target - transform.position).normalized;
            if (direction != Vector3.zero) {
                var targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
        currentIndex++;

        if (currentIndex >= currentPath.Count) {
            State = AgentState.WaitingAtTarget;
            StartCoroutine(WaitAndRequestNextPath());
        } else {
            if (currentIndex > 0) {
                var previousNode = currentPath[currentIndex - 1];
                var previousPosition = new Vector3(previousNode.worldPosition.x, transform.position.y, previousNode.worldPosition.z);
            }
            State = AgentState.WaitingForStep;
            PathSupervisor.Instance.ReportAgentActive(agentId);
        }
    }

    public IEnumerator WaitAndRequestNextPath() {
        yield return new WaitForSeconds(waitAtWaypoint);
        State = AgentState.Idle;
        PathSupervisor.Instance.AssignNextPathToAgent(agentId);
    }

    void OnDestroy() {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
    }
}
