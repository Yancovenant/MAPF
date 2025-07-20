using UnityEngine;
using System.Collections;
using System.Linq;

public class PersonNPC : MonoBehaviour {
    public float speed = 2f, waitTimeStart = 1f, waitTimeMid = 5f, rotationSpeed = 10f;
    
    private Vector3[] waypoints;
    private Animator animator;

    IEnumerator Start() {
        GridManager gridManager = FindAnyObjectByType<GridManager>();
        if (!gridManager.isReady) {
            yield return new WaitUntil(() => gridManager.isReady);
        }
        waypoints = new Vector3[2];
        waypoints[0] = transform.position;
        var nextRoad = gridManager.GetNeighbours(gridManager.NodeFromWorldPoint(new Vector3(transform.position.x, 0, transform.position.z))).FirstOrDefault(n => n.walkable);
        if (nextRoad != null) {
            waypoints[1] = new Vector3(
                nextRoad.worldPosition.x,
                transform.position.y,
                nextRoad.worldPosition.z
            );
            Debug.Log(waypoints[1]);
        }
        animator = GetComponent<Animator>();
        StartCoroutine(_moveCycle());
    }

    private IEnumerator _moveCycle() {
        yield return new WaitForSeconds(waitTimeStart);

        yield return StartCoroutine(_moveTo(1));

        yield return new WaitForSeconds(waitTimeMid);

        yield return StartCoroutine(_moveTo(0));

        yield return null;
        StartCoroutine(_moveCycle());
    }

    private IEnumerator _moveTo(int targetIndex) {
        animator.SetBool("isWalking", true);

        var target = waypoints[targetIndex];

        while (Vector3.Distance(transform.position, target) > 0.1f) {
            var direction = (target - transform.position).normalized;
            if (direction != Vector3.zero) {
                var targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            yield return null;
        }

        animator.SetBool("isWalking", false);
    }
}
